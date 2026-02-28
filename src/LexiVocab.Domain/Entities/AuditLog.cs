using LexiVocab.Domain.Enums;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// Immutable audit trail record. Captures every significant user/system action
/// for security monitoring, compliance, and analytics.
/// 
/// Design decisions:
///  - NOT inheriting BaseEntity: audit logs are append-only, never updated.
///  - UserId is nullable: captures unauthenticated actions (e.g., failed login attempts).
///  - OldValues/NewValues stored as JSON: flexible schema for any entity type.
///  - IpAddress limited to 45 chars to support both IPv4 and IPv6.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who performed the action. Null for anonymous/system events.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Snapshot of user email at time of action for quick lookups without joins.</summary>
    public string? UserEmail { get; set; }

    /// <summary>Categorized action type.</summary>
    public AuditAction Action { get; set; }

    /// <summary>The affected entity type (e.g., "UserVocabulary", "User", "Subscription").</summary>
    public string? EntityType { get; set; }

    /// <summary>The affected entity's primary key.</summary>
    public string? EntityId { get; set; }

    /// <summary>JSON snapshot of previous values (for update/delete operations).</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON snapshot of new values (for create/update operations).</summary>
    public string? NewValues { get; set; }

    /// <summary>Client IP address (supports IPv4 mapped as IPv6, max 45 chars).</summary>
    public string? IpAddress { get; set; }

    /// <summary>Client User-Agent header for device/browser identification.</summary>
    public string? UserAgent { get; set; }

    /// <summary>The MediatR request type name that triggered this action.</summary>
    public string? RequestName { get; set; }

    /// <summary>HTTP request TraceIdentifier for correlating with Serilog logs.</summary>
    public string? TraceId { get; set; }

    /// <summary>Freeform notes or extra context (e.g., "Brute-force detected from IP x.x.x.x").</summary>
    public string? AdditionalInfo { get; set; }

    /// <summary>UTC timestamp when the action occurred. Set once, never modified.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Duration of the operation in milliseconds (for performance auditing).</summary>
    public long? DurationMs { get; set; }

    /// <summary>Whether the operation completed successfully.</summary>
    public bool IsSuccess { get; set; } = true;

    // ─── Navigation ──────────────────────────────────────────
    public User? User { get; set; }
}
