using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

/// <summary>
/// Vocabulary-specific queries optimized for the most common access patterns.
/// </summary>
public interface IVocabularyRepository : IRepository<UserVocabulary>
{
    /// <summary>
    /// Get paginated vocabulary list for a user, optionally filtered by archive status.
    /// </summary>
    Task<(IReadOnlyList<UserVocabulary> Items, int TotalCount)> GetByUserIdAsync(
        Guid userId,
        int page,
        int pageSize,
        bool? isArchived = null,
        string? searchTerm = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get all vocabulary cards due for review today (NextReviewDate &lt;= now, not archived).
    /// This is the critical query — uses NextReviewDate index for sub-ms performance.
    /// </summary>
    Task<IReadOnlyList<UserVocabulary>> GetDueForReviewAsync(
        Guid userId,
        int limit = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Check if a user already has a specific word saved (case-insensitive).
    /// </summary>
    Task<bool> WordExistsForUserAsync(Guid userId, string wordText, CancellationToken ct = default);

    /// <summary>
    /// Get vocabulary count grouped by mastery status for a user.
    /// </summary>
    Task<(int Total, int Active, int Archived, int DueToday)> GetStatsAsync(
        Guid userId, CancellationToken ct = default);
}
