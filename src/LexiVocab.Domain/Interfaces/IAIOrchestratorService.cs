using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Domain.Interfaces;

// Replaces IAIService entirely for new unified system.
public interface IAIOrchestratorService
{
    Task<string> ExecuteTaskAsync(
        AIUseCase useCase,
        Dictionary<string, string> parameters,
        string? providerName = null,
        string? modelId = null,
        bool asJson = false,
        string? customBaseUrl = null,
        string? customApiKey = null,
        CancellationToken ct = default);

    IAsyncEnumerable<string> StreamTaskAsync(
        AIUseCase useCase,
        Dictionary<string, string> parameters,
        string? providerName = null,
        string? modelId = null,
        bool asJson = false,
        string? customBaseUrl = null,
        string? customApiKey = null,
        CancellationToken ct = default);
}
