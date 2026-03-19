using Microsoft.AspNetCore.Http;

namespace Conspectare.Services;

public record OperationResult<T>
{
    public bool IsSuccess { get; init; }
    public T Data { get; init; }
    public string Error { get; init; }
    public List<string> Errors { get; init; }
    public int StatusCode { get; init; } = StatusCodes.Status200OK;

    public static OperationResult<T> Success(T data) =>
        new() { IsSuccess = true, Data = data };

    public static OperationResult<T> Created(T data) =>
        new() { IsSuccess = true, Data = data, StatusCode = StatusCodes.Status201Created };

    public static OperationResult<T> Accepted(T data) =>
        new() { IsSuccess = true, Data = data, StatusCode = StatusCodes.Status202Accepted };

    public static OperationResult<T> NotFound(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status404NotFound };

    public static OperationResult<T> BadRequest(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status400BadRequest };

    public static OperationResult<T> BadRequest(List<string> errors) =>
        new() { IsSuccess = false, Errors = errors, StatusCode = StatusCodes.Status400BadRequest };

    public static OperationResult<T> Conflict(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status409Conflict };

    public static OperationResult<T> Unauthorized(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status401Unauthorized };

    public static OperationResult<T> Forbidden(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status403Forbidden };

    public static OperationResult<T> TooManyRequests(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status429TooManyRequests };

    public static OperationResult<T> ServerError(string message) =>
        new() { IsSuccess = false, Error = message, StatusCode = StatusCodes.Status500InternalServerError };
}
