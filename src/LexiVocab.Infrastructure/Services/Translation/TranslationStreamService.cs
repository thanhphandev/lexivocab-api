using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Services.Translation.Providers;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services.Translation;

public class TranslationStreamService : ITranslationStreamService
{
    private readonly IEnumerable<ITranslationProvider> _providers;
    private readonly ILogger<TranslationStreamService> _logger;

    public TranslationStreamService(IEnumerable<ITranslationProvider> providers, ILogger<TranslationStreamService> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    public async Task<string> TranslateAsync(
        string word, 
        string? context = null, 
        string? provider = null, 
        string? modelId = null, 
        string? from = null, 
        string? to = null, 
        string? customBaseUrl = null, 
        string? customApiKey = null, 
        string? customModel = null, 
        CancellationToken cancellationToken = default)
    {
        string providerType = provider?.ToLowerInvariant() ?? "cloudflare";

        if (providerType.Contains('/'))
        {
            var parts = providerType.Split('/', 2);
            providerType = parts[0];
            modelId = parts[1];
        }

        var selectedProvider = _providers.FirstOrDefault(p => p.CanHandle(providerType));

        if (selectedProvider == null)
        {
            _logger.LogWarning("Provider '{Provider}' not found or unsupported natively. Trying OpenAI-compatible fallback.", providerType);
            selectedProvider = _providers.FirstOrDefault(p => p.GetType().Name == "OpenAiCompatibleTranslationProvider");
            
            if (selectedProvider == null)
            {
                selectedProvider = _providers.FirstOrDefault(p => p.CanHandle("cloudflare"));
            }
        }

        if (selectedProvider != null)
        {
            return await selectedProvider.TranslateAsync(word, context, providerType, modelId, from, to, customBaseUrl, customApiKey, customModel, cancellationToken);
        }

        return $"{{\"error\": \"No translation provider available for {providerType}\"}}";
    }

    public async IAsyncEnumerable<string> StreamTranslateAsync(
        string word, 
        string? context = null, 
        string? provider = null, 
        string? modelId = null, 
        string? from = null, 
        string? to = null, 
        string? customBaseUrl = null, 
        string? customApiKey = null, 
        string? customModel = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string providerType = provider?.ToLowerInvariant() ?? "cloudflare";

        if (providerType.Contains('/'))
        {
            var parts = providerType.Split('/', 2);
            providerType = parts[0];
            modelId = parts[1];
        }

        var selectedProvider = _providers.FirstOrDefault(p => p.CanHandle(providerType));

        if (selectedProvider == null)
        {
            _logger.LogWarning("Provider '{Provider}' not found or unsupported natively. Trying OpenAI-compatible fallback.", providerType);
            // Default to OpenAI Compatible Provider which can handle unknown providers via configuration
            selectedProvider = _providers.FirstOrDefault(p => p.GetType().Name == "OpenAiCompatibleTranslationProvider");
            
            if (selectedProvider == null)
            {
                // Absolute fallback
                selectedProvider = _providers.FirstOrDefault(p => p.CanHandle("cloudflare"));
            }
        }

        if (selectedProvider != null)
        {
            await foreach (var chunk in selectedProvider.StreamTranslateAsync(word, context, providerType, modelId, from, to, customBaseUrl, customApiKey, customModel, cancellationToken))
            {
                yield return chunk;
            }
        }
        else
        {
            yield return $"{{\"error\": \"No translation provider available for {providerType}\"}}";
        }
    }
}
