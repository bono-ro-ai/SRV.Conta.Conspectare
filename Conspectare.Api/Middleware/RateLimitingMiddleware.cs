using System.Collections.Concurrent;
using Conspectare.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Middleware;

/// <summary>
/// Enforces a per-tenant sliding-window rate limit based on the tenant's configured
/// <c>RateLimitPerMin</c> value. Uses an in-process concurrent queue to track request
/// timestamps without requiring a distributed cache.
/// Requests from unauthenticated or unconfigured tenants (TenantId == 0 or limit &lt;= 0)
/// pass through unconditionally.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;

    // One queue per tenant; each entry is a UTC timestamp in milliseconds.
    private readonly ConcurrentDictionary<long, ConcurrentQueue<long>> _windows = new();

    public RateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Checks the tenant's request count within the last 60 seconds and either
    /// forwards the request or returns a 429 Too Many Requests response.
    /// </summary>
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

        // Evict timestamps that have fallen outside the current 60-second window.
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
