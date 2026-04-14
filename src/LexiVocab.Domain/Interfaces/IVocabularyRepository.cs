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
        int page = 1,
        int pageSize = 20,
        bool? isArchived = null,
        string? searchTerm = null,
        Guid? tagId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get paginated vocabulary list for a specific tag.
    /// </summary>
    Task<(IReadOnlyList<UserVocabulary> Items, int TotalCount)> GetByTagIdAsync(
        Guid userId,
        Guid tagId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Get all vocabulary cards due for review today (NextReviewDate &lt;= now, not archived).
    /// Separates New cards (RepetitionCount == 0) and Review cards (RepetitionCount > 0).
    /// </summary>
    Task<IReadOnlyList<UserVocabulary>> GetDueForReviewAsync(
        Guid userId,
        int reviewLimit = 50,
        int newCardLimit = 20,
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

    /// <summary>
    /// Get in-depth advanced vocabulary stats for a user.
    /// </summary>
    Task<(double RetentionRate, double LearningProgress, int WordsLearnedThisWeek, List<string> MostDifficultWords, Dictionary<string, int> CefrSpread)> GetInDepthStatsAsync(
        Guid userId, CancellationToken ct = default);

    Task<int> CountByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Batch check: returns the set of words that already exist for a user (case-insensitive).
    /// Used by BatchImport to avoid N+1 queries.
    /// </summary>
    Task<HashSet<string>> GetExistingWordsAsync(Guid userId, IEnumerable<string> words, CancellationToken ct = default);

    /// <summary>
    /// Gets unique words from UserVocabulary entries that are not yet linked to MasterVocabulary.
    /// </summary>
    Task<List<string>> GetUnlinkedWordsAsync(int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Links all UserVocabulary records with a specific WordText to a MasterVocabulary ID.
    /// </summary>
    Task LinkToMasterAsync(string word, Guid masterId, CancellationToken ct = default);
}
