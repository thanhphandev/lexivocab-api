using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace LexiVocab.Infrastructure.Services.Translation.Providers;

public class BingTranslationProvider : ITranslationProvider
{
    public bool CanHandle(string provider) => provider == "bing";

    public async Task<string> TranslateAsync(
        string word, string? context, string providerType, string? modelId, string? from, string? to, 
        string? customBaseUrl, string? customApiKey, string? customModel, 
        CancellationToken ct)
    {
        var errorObj = new { error = "Bing API requires Authentication Key to be configured in settings to access API", source = "bing" };
        return System.Text.Json.JsonSerializer.Serialize(errorObj);
    }

    public async IAsyncEnumerable<string> StreamTranslateAsync(
        string word, string? context, string providerType, string? modelId, string? from, string? to, 
        string? customBaseUrl, string? customApiKey, string? customModel, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        yield return await TranslateAsync(word, context, providerType, modelId, from, to, customBaseUrl, customApiKey, customModel, ct);
    }
}
