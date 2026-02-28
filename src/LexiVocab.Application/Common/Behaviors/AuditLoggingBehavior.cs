using System.Diagnostics;
using System.Text.Json;
using LexiVocab.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that automatically writes audit log entries for any request
/// implementing <see cref="IAuditedRequest"/>. Captures timing, success/failure, and
/// serializes the request payload as <c>NewValues</c> for full traceability.
///
/// Pipeline order: Validation → AuditLogging → Performance → Handler
///
/// Design decisions:
///  - Fire-and-forget pattern: audit failures are logged but never block the main request.
///  - Request serialization uses safe options to avoid circular references.
///  - The behavior only activates when TRequest implements IAuditedRequest.
/// </summary>
public class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;

    private static readonly JsonSerializerOptions SafeJsonOptions = new()
    {
        WriteIndented = false,
        MaxDepth = 3, // Prevent deep/circular serialization
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
    };

    public AuditLoggingBehavior(
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService,
        ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    {
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only audit requests that opt-in via IAuditedRequest
        if (request is not IAuditedRequest auditedRequest)
        {
            return await next(cancellationToken);
        }

        var sw = Stopwatch.StartNew();
        var isSuccess = true;
        TResponse response;

        try
        {
            response = await next(cancellationToken);

            // Check Result<T> pattern for business-level failures
            isSuccess = IsSuccessResult(response);
        }
        catch
        {
            isSuccess = false;
            sw.Stop();

            // Log the failed attempt, then re-throw
            await SafeLogAsync(auditedRequest, request, isSuccess, sw.ElapsedMilliseconds, cancellationToken);
            throw;
        }

        sw.Stop();
        await SafeLogAsync(auditedRequest, request, isSuccess, sw.ElapsedMilliseconds, cancellationToken);

        return response;
    }

    /// <summary>
    /// Safely attempts to write an audit log. Never throws — audit failures
    /// must not break the main request flow.
    /// </summary>
    private async Task SafeLogAsync(
        IAuditedRequest auditedRequest,
        TRequest request,
        bool isSuccess,
        long durationMs,
        CancellationToken ct)
    {
        try
        {
            var newValues = SafeSerialize(request);

            await _auditLogService.LogAsync(
                action: auditedRequest.AuditAction,
                userId: _currentUserService.UserId,
                userEmail: _currentUserService.Email,
                entityType: auditedRequest.EntityType,
                entityId: auditedRequest.EntityId,
                newValues: newValues,
                additionalInfo: auditedRequest.AdditionalInfo,
                isSuccess: isSuccess,
                durationMs: durationMs,
                ct: ct);
        }
        catch (Exception ex)
        {
            // Audit logging must NEVER break the main flow
            _logger.LogError(ex,
                "⚠️ Failed to write audit log for {RequestName}. Action: {Action}",
                typeof(TRequest).Name,
                auditedRequest.AuditAction);
        }
    }

    /// <summary>Serializes the request payload safely, truncating if too large.</summary>
    private static string? SafeSerialize(TRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, SafeJsonOptions);

            // Truncate to 4KB to prevent oversized audit records
            return json.Length > 4096 ? json[..4096] + "...[truncated]" : json;
        }
        catch
        {
            return $"[Serialization failed for {typeof(TRequest).Name}]";
        }
    }

    /// <summary>
    /// Inspects the response to determine business-level success.
    /// Supports the Result{T} and Result patterns used throughout the app.
    /// </summary>
    private static bool IsSuccessResult(TResponse? response)
    {
        if (response is null) return true;

        // Use reflection to check IsSuccess property on Result<T> / Result
        var type = response.GetType();
        var isSuccessProp = type.GetProperty("IsSuccess");
        if (isSuccessProp?.GetValue(response) is bool success)
        {
            return success;
        }

        return true; // Non-Result types are assumed successful if no exception was thrown
    }
}
