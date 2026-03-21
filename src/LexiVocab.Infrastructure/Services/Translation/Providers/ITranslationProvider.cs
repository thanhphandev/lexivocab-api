using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LexiVocab.Infrastructure.Services.Translation.Providers;

public interface ITranslationProvider
{
    bool CanHandle(string provider);
    
    Task<string> TranslateAsync(
        string word, 
        string? context, 
        string providerType,
        string? modelId, 
        string? from, 
        string? to, 
        string? customBaseUrl, 
        string? customApiKey, 
        string? customModel, 
        CancellationToken ct)
    {
        throw new NotSupportedException($"Non-streaming translation not supported natively by {this.GetType().Name}.");
    }

    IAsyncEnumerable<string> StreamTranslateAsync(
        string word, 
        string? context, 
        string providerType,
        string? modelId, 
        string? from, 
        string? to, 
        string? customBaseUrl, 
        string? customApiKey, 
        string? customModel, 
        CancellationToken ct)
    {
        throw new NotSupportedException($"Streaming translation not supported natively by {this.GetType().Name}.");
    }
}
