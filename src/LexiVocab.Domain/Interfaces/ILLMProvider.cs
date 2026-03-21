using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LexiVocab.Domain.Models.AI;

namespace LexiVocab.Domain.Interfaces;

public interface ILLMProvider
{
    string ProviderName { get; }
    int Priority { get; }
    bool CanHandle(string provider);

    Task<string> ExecuteAsync(
        LlmRequest request,
        string? customBaseUrl = null,
        string? customApiKey = null,
        CancellationToken ct = default);

    IAsyncEnumerable<string> StreamExecuteAsync(
        LlmRequest request,
        string? customBaseUrl = null,
        string? customApiKey = null,
        CancellationToken ct = default);
}
