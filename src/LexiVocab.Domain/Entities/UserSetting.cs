using LexiVocab.Domain.Common;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// User-specific extension settings, synced across devices.
/// 1-to-1 relationship with User.
/// ExcludedDomains stored as List&lt;string&gt; mapped to PostgreSQL JSONB for flexible querying.
/// </summary>
public class UserSetting : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Whether word highlighting is enabled in the Chrome extension.</summary>
    public bool IsHighlightEnabled { get; set; } = true;

    /// <summary>Hex color code for highlight (e.g., "#FFD700").</summary>
    public string HighlightColor { get; set; } = "#FFD700";

    /// <summary>Domains excluded from highlighting (e.g., ["facebook.com", "youtube.com"]). Stored as JSONB.</summary>
    public List<string> ExcludedDomains { get; set; } = [];

    /// <summary>Daily review goal (number of cards). Default 20.</summary>
    public int DailyGoal { get; set; } = 20;

    // Navigation
    public User User { get; set; } = null!;
}
