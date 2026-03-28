using Conspectare.Services;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Extensions;

/// <summary>
/// Extension methods that convert a domain <see cref="OperationResult{T}"/> into an
/// appropriate <see cref="IActionResult"/>, mapping success status codes (201, 202, 200)
/// and failure status codes to RFC 7807 ProblemDetails responses.
/// </summary>
public static class OperationResultExtensions
{
    /// <summary>
    /// Converts <paramref name="result"/> to an <see cref="IActionResult"/>.
    /// Successful results are wrapped in the matching HTTP status object result.
    /// Failed results are mapped to a <see cref="ProblemDetails"/> body with a human-readable
    /// title derived from the status code, and an optional structured error list.
    /// </summary>
    public static IActionResult ToActionResult<T>(this OperationResult<T> result)
    {
        if (result.IsSuccess)
        {
            return result.StatusCode switch
            {
                StatusCodes.Status201Created => new ObjectResult(result.Data) { StatusCode = StatusCodes.Status201Created },
                StatusCodes.Status202Accepted => new ObjectResult(result.Data) { StatusCode = StatusCodes.Status202Accepted },
                _ => new OkObjectResult(result.Data)
            };
        }

        var problemDetails = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{result.StatusCode}",
            Status = result.StatusCode,
            Title = result.StatusCode switch
            {
                StatusCodes.Status400BadRequest => "Bad Request",
                StatusCodes.Status401Unauthorized => "Unauthorized",
                StatusCodes.Status403Forbidden => "Forbidden",
                StatusCodes.Status404NotFound => "Not Found",
                StatusCodes.Status409Conflict => "Conflict",
                StatusCodes.Status429TooManyRequests => "Too Many Requests",
                _ => "Internal Server Error"
            },
            Detail = result.Error
        };

        // Attach field-level validation errors when present so clients can display them.
        if (result.Errors is { Count: > 0 })
            problemDetails.Extensions["errors"] = result.Errors;

        return new ObjectResult(problemDetails) { StatusCode = result.StatusCode };
    }
}
