using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;

namespace LexiVocab.Infrastructure.Services.Translation.Providers;

public class LlmTranslationProvider : ITranslationProvider
{
    private readonly IAIOrchestratorService _orchestrator;

    public LlmTranslationProvider(IAIOrchestratorService orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public int Priority => -1; // Fallback or priority for LLMs

    // Delegate to LLM Orchestrator any provider not explicitly meant for traditional translators
    public bool CanHandle(string provider) => provider != "google" && provider != "lingva" && provider != "bing" && provider != "chrome-ai";

    public async Task<string> TranslateAsync(
        string word, string? context, string providerType, string? modelId, string? from, string? to, 
        string? customBaseUrl, string? customApiKey, string? customModel, 
        CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>
        {
            { "word", word },
            { "context", context ?? "" },
            { "from", LanguageMapper.GetName(from, true) },
            { "to", LanguageMapper.GetName(to, false) }
        };

        // Forward to the unified Orchestrator
        return await _orchestrator.ExecuteTaskAsync(
            AIUseCase.Translation, 
            parameters, 
            providerType, 
            customModel ?? modelId, // Override modelId if customModel is provided
            true, // We assume JSON return for translation
            customBaseUrl, 
            customApiKey, 
            ct);
    }

    public async IAsyncEnumerable<string> StreamTranslateAsync(
        string word, string? context, string providerType, string? modelId, string? from, string? to, 
        string? customBaseUrl, string? customApiKey, string? customModel, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>
        {
            { "word", word },
            { "context", context ?? "" },
            { "from", LanguageMapper.GetName(from, true) },
            { "to", LanguageMapper.GetName(to, false) }
        };

        await foreach (var chunk in _orchestrator.StreamTaskAsync(
            AIUseCase.Translation, 
            parameters, 
            providerType, 
            customModel ?? modelId, 
            true, 
            customBaseUrl, 
            customApiKey, 
            ct))
        {
            yield return chunk;
        }
    }
}
