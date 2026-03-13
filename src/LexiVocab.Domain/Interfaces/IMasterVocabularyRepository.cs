using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

/// <summary>
/// Master vocabulary lookup repository.
/// </summary>
public interface IMasterVocabularyRepository : IRepository<MasterVocabulary>
{
    Task<MasterVocabulary?> GetByWordAsync(string word, CancellationToken ct = default);
    Task<IReadOnlyList<MasterVocabulary>> SearchAsync(string query, int limit = 10, CancellationToken ct = default);
    Task<(IReadOnlyList<MasterVocabulary> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? searchQuery = null, CancellationToken ct = default);

    /// <summary>
    /// Batch lookup: returns master vocabulary entries for multiple words in a single query.
    /// Used by BatchImport to avoid N+1 queries.
    /// </summary>
    Task<Dictionary<string, MasterVocabulary>> GetByWordsAsync(IEnumerable<string> words, CancellationToken ct = default);

    /// <summary>
    /// Finds words that are missing phonetics or audio and need enrichment from external APIs.
    /// </summary>
    Task<IReadOnlyList<MasterVocabulary>> GetPendingEnrichmentAsync(int limit = 50, CancellationToken ct = default);
}
