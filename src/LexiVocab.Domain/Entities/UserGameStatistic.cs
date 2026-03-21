using LexiVocab.Domain.Common;

namespace LexiVocab.Domain.Entities;

public class UserGameStatistic : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public int TotalXP { get; set; } = 0;
    public int CurrentLevel { get; set; } = 1;
    public int LongestStreak { get; set; } = 0;
    public int CurrentStreak { get; set; } = 0;
    
    /// <summary>Number of hearts (lives) remaining.</summary>
    public int Hearts { get; set; } = 5;

    public DateTime? LastPlayedAt { get; set; }
}
