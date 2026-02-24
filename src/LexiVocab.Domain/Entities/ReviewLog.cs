using LexiVocab.Domain.Common;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// Immutable audit log for each flashcard review event.
/// Append-only table — expected to grow to hundreds of millions of rows.
/// UserId is denormalized to avoid expensive JOINs for analytics (heatmap, streak).
/// Uses BRIN index on ReviewedAt for time-series queries.
/// </summary>
public class ReviewLog : BaseEntity
{
    public Guid UserVocabularyId { get; set; }

    /// <summary>Denormalized UserId for O(1) per-user analytics without JOIN through UserVocabulary.</summary>
    public Guid UserId { get; set; }

    /// <summary>SM-2 quality rating (0–5) self-evaluated by user.</summary>
    public QualityScore QualityScore { get; set; }

    /// <summary>Response time in milliseconds — measures recall speed.</summary>
    public int? TimeSpentMs { get; set; }

    /// <summary>Exact timestamp of this review. BRIN indexed for heatmap queries.</summary>
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public UserVocabulary UserVocabulary { get; set; } = null!;
    public User User { get; set; } = null!;
}
