using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

/// <summary>
/// Domain interface for fetching vocabulary data from authoritative external sources.
/// </summary>
public interface IDictionaryService
{
    /// <summary>
    /// Fetches word details (phonetics, audio, part of speech) from an external provider.
    /// </summary>
    Task<MasterVocabulary?> FetchWordDefinitionAsync(string word, CancellationToken ct = default);
}
