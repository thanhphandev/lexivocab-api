namespace LexiVocab.Application.DTOs.Admin;

/// <summary>
/// Represents a single audit log entry for API responses.
/// Maps from the AuditLog entity with Action serialized as string.
/// </summary>
public record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    string Action,
    string? EntityType,
    string? EntityId,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    string? UserAgent,
    string? RequestName,
    string? TraceId,
    string? AdditionalInfo,
    bool IsSuccess,
    long? DurationMs,
    DateTime Timestamp);
