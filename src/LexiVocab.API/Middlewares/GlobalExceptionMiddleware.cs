using System.Net;
using System.Text.Json;
using FluentValidation;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Models;

namespace LexiVocab.API.Middlewares;

/// <summary>
/// Global exception handler middleware. Catches ALL unhandled exceptions and returns
/// a structured JSON error response. Prevents stack traces from leaking to clients.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, errorCode) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage)),
                ErrorCode.VALIDATION_FAILED),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Unauthorized access.",
                ErrorCode.AUTH_INVALID_TOKEN),

            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                "Resource not found.",
                ErrorCode.RESOURCE_NOT_FOUND),

            OperationCanceledException => (
                HttpStatusCode.BadRequest,
                "The request was cancelled.",
                ErrorCode.UNKNOWN_ERROR),

            TimeoutException or System.Net.Http.HttpRequestException => (
                HttpStatusCode.ServiceUnavailable,
                "The service is temporarily unavailable. Please try again later.",
                ErrorCode.SERVICE_UNAVAILABLE),

            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred. Please try again later.",
                ErrorCode.INTERNAL_SERVER_ERROR)
        };

        // Log with full details — but return sanitized message to client
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "❌ Unhandled exception: {Message}", exception.Message);
        }
        else
        {
            _logger.LogWarning("⚠️ Handled exception ({StatusCode}): {Message}", statusCode, exception.Message);
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            success = false,
            error = message,
            errorCode = errorCode.ToString(),
            statusCode = (int)statusCode,
            traceId = context.TraceIdentifier,
            timestamp = DateTime.UtcNow,
            details = exception is ValidationException vex ? new ErrorDetails 
            {
                ValidationErrors = vex.Errors.Select(e => new ValidationError 
                {
                    Field = e.PropertyName,
                    ErrorCode = string.IsNullOrWhiteSpace(e.ErrorCode) ? "VAL_INVALID" : e.ErrorCode,
                    Message = e.ErrorMessage
                }).ToList()
            } : null
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }));
    }
}
