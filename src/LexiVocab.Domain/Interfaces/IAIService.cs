using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

/// <summary>
/// Domain interface for AI-powered vocabulary enrichment and generation.
/// Provides more flexibility than standard dictionary lookups.
/// </summary>
public interface IAIService
{
    /// <summary>
    /// Uses AI to enrich a word's definition, phonetics, and example sentences.
    /// </summary>
    Task<MasterVocabulary?> EnrichWordAsync(string word, CancellationToken ct = default);

    /// <summary>
    /// Explains the usage and nuances of a word.
    /// </summary>
    Task<string?> ExplainUsageAsync(string word, string? context = null, CancellationToken ct = default);

    /// <summary>
    /// Returns synonyms, antonyms, and collocations for a word.
    /// </summary>
    Task<string?> GetRelatedWordsAsync(string word, CancellationToken ct = default);

    /// <summary>
    /// Generates a multiple-choice quiz for a word.
    /// </summary>
    Task<string?> GenerateQuizAsync(string word, CancellationToken ct = default);

    /// <summary>
    /// Generates mnemonics or memory aids for a word.
    /// </summary>
    Task<string?> GenerateMnemonicAsync(string word, string meaning, CancellationToken ct = default);
}
