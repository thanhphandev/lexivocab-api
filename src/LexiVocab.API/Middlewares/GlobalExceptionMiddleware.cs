using System.Net;
using System.Text.Json;
using FluentValidation;

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
        var (statusCode, message) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage))),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Unauthorized access."),

            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                "Resource not found."),

            OperationCanceledException => (
                HttpStatusCode.BadRequest,
                "The request was cancelled."),

            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred. Please try again later.")
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
            statusCode = (int)statusCode,
            traceId = context.TraceIdentifier,
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}
