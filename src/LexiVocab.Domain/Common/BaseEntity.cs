namespace LexiVocab.Domain.Common;

/// <summary>
/// Base entity with audit fields. All domain entities inherit from this.
/// Uses UUID (Guid) as primary key for distributed-system friendliness and anti-enumeration.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
