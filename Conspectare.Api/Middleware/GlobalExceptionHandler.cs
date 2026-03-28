using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Middleware;

/// <summary>
/// Catches all unhandled exceptions and maps them to RFC 7807 ProblemDetails responses.
/// Distinguishes between cancelled requests (499) and genuine server errors (500).
/// In development, the exception message and trace ID are included in the response body.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    /// <summary>
    /// Attempts to handle the given exception by writing an appropriate problem response.
    /// Always returns <c>true</c> to prevent further exception-handler chaining.
    /// </summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            // 499 is the nginx convention for "client closed request"; no body is needed.
            _logger.LogInformation("Request cancelled for {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = 499;
            return true;
        }

        _logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Type = "https://httpstatuses.com/500",
            Title = "Internal Server Error",
            Status = StatusCodes.Status500InternalServerError,
            Instance = $"{context.Request.Method} {context.Request.Path}",
            // Expose the raw message only in development to avoid leaking internals.
            Detail = _env.IsDevelopment() ? exception.Message : null
        };

        if (_env.IsDevelopment())
            problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
