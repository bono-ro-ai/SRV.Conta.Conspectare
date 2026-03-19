using Conspectare.Services;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Extensions;

public static class OperationResultExtensions
{
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

        if (result.Errors is { Count: > 0 })
            problemDetails.Extensions["errors"] = result.Errors;

        return new ObjectResult(problemDetails) { StatusCode = result.StatusCode };
    }
}
