using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

/// <summary>
/// Domain interface for AI-powered vocabulary enrichment and generation.
/// Provides more flexibility than standard dictionary lookups.
/// </summary>
public interface IAIService
{

    /// <summary>
    /// Returns synonyms, antonyms, and collocations for a word.
    /// </summary>
    Task<string?> GetRelatedWordsAsync(string word, string? targetLanguage = null, string? userLanguage = null, CancellationToken ct = default);

    /// <summary>
    /// Streams the explanation of usage and nuances of a word.
    /// </summary>
    IAsyncEnumerable<string> StreamExplainUsageAsync(string word, string? context = null, bool asJson = false, string? targetLanguage = null, string? userLanguage = null, CancellationToken ct = default);

    /// <summary>
    /// Generates a multiple-choice quiz for a word.
    /// </summary>
    Task<string?> GenerateQuizAsync(string word, string? targetLanguage = null, string? userLanguage = null, CancellationToken ct = default);
}
