using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Models;

namespace LexiVocab.Application.Common;

public interface IResult
{
    bool IsSuccess { get; }
    ErrorCode ErrorCode { get; }
    ErrorDetails? Details { get; }
}

public interface IResult<TSelf> : IResult where TSelf : IResult<TSelf>
{
    static abstract TSelf Failure(string error, int statusCode, ErrorCode errorCode, ErrorDetails? details = null);
}

/// <summary>
/// Generic result wrapper for all CQRS operations.
/// Eliminates exceptions for expected business failures — use pattern matching instead.
/// </summary>
public class Result<T> : IResult<Result<T>>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public string? Error { get; }
    public int StatusCode { get; }
    public ErrorCode ErrorCode { get; }
    public ErrorDetails? Details { get; }

    private Result(bool isSuccess, T? data, string? error, int statusCode, ErrorCode errorCode, ErrorDetails? details)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Details = details;
    }

    public static Result<T> Success(T data) => new(true, data, null, 200, ErrorCode.UNKNOWN_ERROR, null);
    public static Result<T> Created(T data) => new(true, data, null, 201, ErrorCode.UNKNOWN_ERROR, null);
    
    public static Result<T> Failure(string error, int statusCode = 400, ErrorCode errorCode = ErrorCode.VALIDATION_FAILED, ErrorDetails? details = null) 
        => new(false, default, error, statusCode, errorCode, details);
        
    public static Result<T> NotFound(string error = "Resource not found", ErrorCode errorCode = ErrorCode.RESOURCE_NOT_FOUND) 
        => new(false, default, error, 404, errorCode, null);
        
    public static Result<T> Unauthorized(string error = "Unauthorized", ErrorCode errorCode = ErrorCode.AUTH_INVALID_TOKEN) 
        => new(false, default, error, 401, errorCode, null);
        
    public static Result<T> Forbidden(string error = "Forbidden", ErrorCode errorCode = ErrorCode.AUTHZ_RESOURCE_FORBIDDEN) 
        => new(false, default, error, 403, errorCode, null);
        
    public static Result<T> Conflict(string error, ErrorCode errorCode = ErrorCode.RESOURCE_CONFLICT) 
        => new(false, default, error, 409, errorCode, null);
}

/// <summary>
/// Non-generic result for commands that don't return data.
/// </summary>
public class Result : IResult<Result>
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public int StatusCode { get; }
    public ErrorCode ErrorCode { get; }
    public ErrorDetails? Details { get; }

    private Result(bool isSuccess, string? error, int statusCode, ErrorCode errorCode, ErrorDetails? details)
    {
        IsSuccess = isSuccess;
        Error = error;
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Details = details;
    }

    public static Result Success() => new(true, null, 200, ErrorCode.UNKNOWN_ERROR, null);
    
    public static Result Failure(string error, int statusCode = 400, ErrorCode errorCode = ErrorCode.VALIDATION_FAILED, ErrorDetails? details = null) 
        => new(false, error, statusCode, errorCode, details);
        
    public static Result NotFound(string error = "Resource not found", ErrorCode errorCode = ErrorCode.RESOURCE_NOT_FOUND) 
        => new(false, error, 404, errorCode, null);
        
    public static Result Conflict(string error, ErrorCode errorCode = ErrorCode.RESOURCE_CONFLICT) 
        => new(false, error, 409, errorCode, null);
}
