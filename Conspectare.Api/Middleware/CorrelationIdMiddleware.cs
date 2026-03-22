using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Conspectare.Api.Middleware;

public partial class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const int MaxCorrelationIdLength = 64;
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var incoming = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        var correlationId = IsValidCorrelationId(incoming)
            ? incoming!
            : Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }

    private static bool IsValidCorrelationId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.Length <= MaxCorrelationIdLength
               && SafeIdRegex().IsMatch(value);
    }

    [GeneratedRegex(@"^[a-zA-Z0-9\-_.]+$")]
    private static partial Regex SafeIdRegex();
}
