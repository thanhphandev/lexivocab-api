using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// Service abstraction for writing audit log entries.
/// Decouples the Application layer from Infrastructure persistence details.
/// </summary>
public interface IAuditLogService
{
    /// <summary>Record a single audit event.</summary>
    Task LogAsync(
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
        CancellationToken ct = default);

    /// <summary>Record multiple audit events in a single batch.</summary>
    Task LogBatchAsync(IEnumerable<AuditLog> logs, CancellationToken ct = default);
}
