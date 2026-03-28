using Microsoft.AspNetCore.Http;

namespace Conspectare.Services;

/// <summary>
/// Discriminated union representing the outcome of a service operation.
/// Carries either a success payload or error information together with an HTTP status code,
/// allowing controllers to map results to responses without try/catch chains.
/// </summary>
public record OperationResult<T>
{
    public bool IsSuccess { get; init; }
    public T Data { get; init; }
    public string Error { get; init; }
    public List<string> Errors { get; init; }
    public int StatusCode { get; init; } = StatusCodes.Status200OK;

    /// <summary>Returns a 200 OK success result containing <paramref name="data"/>.</summary>
    public static OperationResult<T> Success(T data) =>
        new() { IsSuccess = true, Data = data };

    /// <summary>Returns a 201 Created success result containing <paramref name="data"/>.</summary>
    public static OperationResult<T> Created(T data) =>
        new() { IsSuccess = true, Data = data, StatusCode = StatusCodes.Status201Created };

    /// <summary>Returns a 202 Accepted success result containing <paramref name="data"/>.</summary>
    public static OperationResult<T> Accepted(T data) =>
        new() { IsSuccess = true, Data = data, StatusCode = StatusCodes.Status202Accepted };

    /// <summary>Returns a 404 Not Found failure result with the given error <paramref name="message"/>.</summary>
    public static OperationResult<T> NotFound(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status404NotFound };

    /// <summary>Returns a 400 Bad Request failure result with a single error <paramref name="message"/>.</summary>
    public static OperationResult<T> BadRequest(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status400BadRequest };

    /// <summary>Returns a 400 Bad Request failure result with multiple validation <paramref name="errors"/>.</summary>
    public static OperationResult<T> BadRequest(List<string> errors) =>
        new() { IsSuccess = false, Errors = errors, StatusCode = StatusCodes.Status400BadRequest };

    /// <summary>Returns a 409 Conflict failure result with the given error <paramref name="message"/>.</summary>
    public static OperationResult<T> Conflict(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status409Conflict };

    /// <summary>Returns a 401 Unauthorized failure result with the given error <paramref name="message"/>.</summary>
    public static OperationResult<T> Unauthorized(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status401Unauthorized };

    /// <summary>Returns a 403 Forbidden failure result with the given error <paramref name="message"/>.</summary>
    public static OperationResult<T> Forbidden(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status403Forbidden };

    /// <summary>Returns a 429 Too Many Requests failure result with the given error <paramref name="message"/>.</summary>
    public static OperationResult<T> TooManyRequests(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status429TooManyRequests };

    /// <summary>Returns a 500 Internal Server Error failure result with the given error <paramref name="message"/>.</summary>
    public static OperationResult<T> ServerError(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status500InternalServerError };
}
