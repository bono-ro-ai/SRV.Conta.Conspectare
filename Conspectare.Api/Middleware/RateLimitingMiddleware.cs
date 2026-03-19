using System.Collections.Concurrent;
using Conspectare.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ConcurrentDictionary<long, ConcurrentQueue<long>> _windows = new();

    public RateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (tenantContext.TenantId == 0)
        {
            await _next(context);
            return;
        }

        var limit = tenantContext.RateLimitPerMin;

        if (limit <= 0)
        {
            await _next(context);
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - 60_000;

        var queue = _windows.GetOrAdd(tenantContext.TenantId, _ => new ConcurrentQueue<long>());

        // Prune entries older than 60 seconds
        while (queue.TryPeek(out var oldest) && oldest < windowStart)
        {
            queue.TryDequeue(out _);
        }

        if (queue.Count >= limit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Type = "https://httpstatuses.com/429",
                Title = "Too Many Requests",
                Status = StatusCodes.Status429TooManyRequests,
                Detail = $"Rate limit of {limit} requests per minute exceeded."
            };

            await context.Response.WriteAsJsonAsync(problem);
            return;
        }

        queue.Enqueue(now);
        await _next(context);
    }
}
