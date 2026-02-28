using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace LexiVocab.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IAuditLogService"/>.
/// Automatically captures HTTP context metadata (IP, User-Agent, TraceId)
/// from the current request for each audit record.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _repository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(IAuditLogRepository repository, IHttpContextAccessor httpContextAccessor)
    {
        _repository = repository;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        AuditAction action,
        Guid? userId = null,
        string? userEmail = null,
        string? entityType = null,
        string? entityId = null,
        string? oldValues = null,
        string? newValues = null,
        string? additionalInfo = null,
        bool isSuccess = true,
        long? durationMs = null,
        CancellationToken ct = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var auditLog = new AuditLog
        {
            UserId = userId,
            UserEmail = userEmail,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = GetClientIpAddress(httpContext),
            UserAgent = GetUserAgent(httpContext),
            RequestName = httpContext?.GetEndpoint()?.DisplayName,
            TraceId = httpContext?.TraceIdentifier,
            AdditionalInfo = additionalInfo,
            IsSuccess = isSuccess,
            DurationMs = durationMs,
            Timestamp = DateTime.UtcNow
        };

        await _repository.AddAsync(auditLog, ct);
    }

    public async Task LogBatchAsync(IEnumerable<AuditLog> logs, CancellationToken ct = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var ip = GetClientIpAddress(httpContext);
        var userAgent = GetUserAgent(httpContext);
        var traceId = httpContext?.TraceIdentifier;

        // Enrich each log with HTTP context if not already set
        foreach (var log in logs)
        {
            log.IpAddress ??= ip;
            log.UserAgent ??= userAgent;
            log.TraceId ??= traceId;
        }

        await _repository.AddRangeAsync(logs, ct);
    }

    /// <summary>
    /// Extracts the real client IP, respecting X-Forwarded-For for reverse-proxy setups.
    /// </summary>
    private static string? GetClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext is null) return null;

        // Check X-Forwarded-For first (behind load balancer / reverse proxy)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // Take the first IP (original client)
            var ip = forwardedFor.Split(',', StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(ip))
                return ip.Length > 45 ? ip[..45] : ip;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static string? GetUserAgent(HttpContext? httpContext)
    {
        var ua = httpContext?.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(ua)) return null;
        return ua.Length > 512 ? ua[..512] : ua;
    }
}
