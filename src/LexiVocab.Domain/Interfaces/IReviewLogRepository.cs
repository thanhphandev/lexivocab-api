using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

/// <summary>
/// Review log repository for analytics-heavy queries (heatmap, streaks).
/// Optimized for time-series data with BRIN indexing on ReviewedAt.
/// </summary>
public interface IReviewLogRepository : IRepository<ReviewLog>
{
    /// <summary>
    /// Get review counts grouped by date for heatmap rendering (GitHub-style contribution graph).
    /// </summary>
    Task<IReadOnlyList<(DateOnly Date, int Count)>> GetHeatmapDataAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);

    /// <summary>
    /// Get the current consecutive-day streak for a user.
    /// </summary>
    Task<int> GetCurrentStreakAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get review history for a specific vocabulary card.
    /// </summary>
    Task<IReadOnlyList<ReviewLog>> GetByVocabularyIdAsync(
        Guid userVocabularyId,
        CancellationToken ct = default);

    /// <summary>
    /// Get total reviews and average quality score for a user within a date range.
    /// </summary>
    Task<(int TotalReviews, double AvgQuality, int TotalTimeMs)> GetPeriodStatsAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
}
