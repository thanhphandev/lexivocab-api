using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Domain.Interfaces;

/// <summary>
/// Repository for audit log persistence. Supports write-heavy, read-optimized patterns.
/// No update/delete — audit logs are immutable by design.
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>Append a single audit record.</summary>
    Task AddAsync(AuditLog log, CancellationToken ct = default);

    /// <summary>Append multiple audit records in a single round-trip (batch insert).</summary>
    Task AddRangeAsync(IEnumerable<AuditLog> logs, CancellationToken ct = default);

    /// <summary>Query audit logs with filtering and pagination.</summary>
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPagedAsync(
        Guid? userId = null,
        AuditAction? action = null,
        string? entityType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>Get recent login attempts for an IP (for brute-force detection).</summary>
    Task<int> CountRecentFailedLoginsAsync(string ipAddress, TimeSpan window, CancellationToken ct = default);
}
