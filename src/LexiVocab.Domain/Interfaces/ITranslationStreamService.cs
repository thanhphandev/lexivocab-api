using System.Collections.Generic;
using System.Threading;

namespace LexiVocab.Domain.Interfaces;

/// <summary>
/// Domain interface for flexible translation streaming from various providers.
/// </summary>
public interface ITranslationStreamService
{
    /// <summary>
    /// Translates a word and returns the final JSON string result immediately without streaming (Suitable for Google/Bing directly).
    /// </summary>
    Task<string> TranslateAsync(
        string word, 
        string? context = null, 
        string? provider = null, 
        string? modelId = null, 
        string? from = null, 
        string? to = null, 
        string? customBaseUrl = null, 
        string? customApiKey = null, 
        string? customModel = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a translation from the AI model or API chunk by chunk. Supports BYOK Custom Providers.
    /// </summary>
    IAsyncEnumerable<string> StreamTranslateAsync(
        string word, 
        string? context = null, 
        string? provider = null, 
        string? modelId = null, 
        string? from = null, 
        string? to = null, 
        string? customBaseUrl = null, 
        string? customApiKey = null, 
        string? customModel = null, 
        CancellationToken cancellationToken = default);
}
