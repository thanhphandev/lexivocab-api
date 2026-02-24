using LexiVocab.Domain.Common;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// The HEART of the system ❤️ — a user's personal flashcard.
/// Contains denormalized WordText for fast reads without JOINing MasterVocabulary.
/// SM-2 (SuperMemo-2) fields drive the Spaced Repetition scheduling.
/// Expected to be the largest table (millions of rows).
/// </summary>
public class UserVocabulary : BaseEntity
{
    // ─── Foreign Keys ────────────────────────────────────────────
    public Guid UserId { get; set; }
    public Guid? MasterVocabularyId { get; set; }

    // ─── Vocabulary Data ─────────────────────────────────────────
    /// <summary>Denormalized word text for fast retrieval without JOIN.</summary>
    public string WordText { get; set; } = string.Empty;

    /// <summary>User-defined meaning (Vietnamese) or AI-generated translation.</summary>
    public string? CustomMeaning { get; set; }

    /// <summary>Original sentence context where user highlighted the word on the web.</summary>
    public string? ContextSentence { get; set; }

    /// <summary>Source URL of the webpage where the word was captured.</summary>
    public string? SourceUrl { get; set; }

    // ─── SM-2 Spaced Repetition Fields ───────────────────────────
    /// <summary>Number of consecutive correct responses.</summary>
    public int RepetitionCount { get; set; }

    /// <summary>Easiness Factor (EF) — starts at 2.5, minimum 1.3. Controls interval growth rate.</summary>
    public double EasinessFactor { get; set; } = 2.5;

    /// <summary>Current interval in days until next review.</summary>
    public int IntervalDays { get; set; }

    /// <summary>Scheduled next review date. INDEXED for sub-millisecond daily review queries.</summary>
    public DateTime NextReviewDate { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp of most recent review.</summary>
    public DateTime? LastReviewedAt { get; set; }

    // ─── Status ──────────────────────────────────────────────────
    /// <summary>Soft delete / "Mastered" flag. Hidden from review queue but preserved for analytics.</summary>
    public bool IsArchived { get; set; }

    // ─── Navigation ──────────────────────────────────────────────
    public User User { get; set; } = null!;
    public MasterVocabulary? MasterVocabulary { get; set; }
    public ICollection<ReviewLog> ReviewLogs { get; set; } = [];
}
