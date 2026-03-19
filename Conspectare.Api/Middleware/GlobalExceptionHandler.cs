using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
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
