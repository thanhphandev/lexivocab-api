using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services.AI;

public class AIOrchestratorService : IAIOrchestratorService
{
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly IEnumerable<ILLMProvider> _providers;
    private readonly ILogger<AIOrchestratorService> _logger;

    public AIOrchestratorService(
        IPromptTemplateService promptTemplateService,
        IEnumerable<ILLMProvider> providers,
        ILogger<AIOrchestratorService> logger)
    {
        _promptTemplateService = promptTemplateService;
        _providers = providers;
        _logger = logger;
    }

    private ILLMProvider GetProvider(string? providerName)
    {
        string pName = providerName?.ToLowerInvariant() ?? "openrouter";
        var selectedProvider = _providers.FirstOrDefault(p => p.CanHandle(pName));

        if (selectedProvider == null)
        {
            _logger.LogWarning("Provider '{Provider}' not found. Trying fallback.", pName);
            selectedProvider = _providers.OrderByDescending(p => p.Priority).FirstOrDefault();
            
            if (selectedProvider == null)
            {
                throw new InvalidOperationException("No LLM Providers registered.");
            }
        }
        return selectedProvider;
    }

    public async Task<string> ExecuteTaskAsync(
        AIUseCase useCase,
        Dictionary<string, string> parameters,
        string? providerName = null,
        string? modelId = null,
        bool asJson = true,
        string? customBaseUrl = null,
        string? customApiKey = null,
        CancellationToken ct = default)
    {
        string pName = providerName?.ToLowerInvariant() ?? "openrouter";
        var request = await _promptTemplateService.BuildRequestAsync(useCase, parameters, modelId, asJson);
        request.ProviderName = pName;
        var provider = GetProvider(pName);
        return await provider.ExecuteAsync(request, customBaseUrl, customApiKey, ct);
    }

    public async IAsyncEnumerable<string> StreamTaskAsync(
        AIUseCase useCase,
        Dictionary<string, string> parameters,
        string? providerName = null,
        string? modelId = null,
        bool asJson = true,
        string? customBaseUrl = null,
        string? customApiKey = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        string pName = providerName?.ToLowerInvariant() ?? "openrouter";
        var request = await _promptTemplateService.BuildRequestAsync(useCase, parameters, modelId, asJson);
        request.ProviderName = pName;
        var provider = GetProvider(pName);
        
        await foreach (var chunk in provider.StreamExecuteAsync(request, customBaseUrl, customApiKey, ct))
        {
            yield return chunk;
        }
    }
}
