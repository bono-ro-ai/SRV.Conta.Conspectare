using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Conspectare.Api.Middleware;

/// <summary>
/// Propagates a correlation ID through the request pipeline.
/// If the incoming request carries a valid <c>X-Correlation-Id</c> header it is reused;
/// otherwise a new ID is derived from the current <see cref="Activity"/> or a fresh GUID.
/// The resolved ID is echoed back in the response header and injected into every log scope
/// produced during the request, enabling end-to-end tracing across services.
/// </summary>
public partial class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const int MaxCorrelationIdLength = 64;
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Resolves or generates a correlation ID, attaches it to the response, opens a log scope,
    /// and forwards the request to the next middleware.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var incoming = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();

        // Accept the caller-supplied ID only if it passes the safe-character check;
        // fall back to the current Activity ID (distributed tracing) or a fresh GUID.
        var correlationId = IsValidCorrelationId(incoming)
            ? incoming!
            : Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

        // Register the header on the response before the body starts writing.
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

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> is non-empty, within the length cap,
    /// and contains only alphanumeric characters plus hyphens, dots, and underscores.
    /// </summary>
    private static bool IsValidCorrelationId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.Length <= MaxCorrelationIdLength
               && SafeIdRegex().IsMatch(value);
    }

    [GeneratedRegex(@"^[a-zA-Z0-9\-_.]+$")]
    private static partial Regex SafeIdRegex();
}
