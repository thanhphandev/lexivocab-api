using System.Runtime.CompilerServices;
using System.Text.Json;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Domain.Models.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.ClientModel;

namespace LexiVocab.Infrastructure.Services.AI.Providers;

public class OpenAiCompatibleLLMProvider : ILLMProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiCompatibleLLMProvider> _logger;

    public OpenAiCompatibleLLMProvider(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ILogger<OpenAiCompatibleLLMProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string ProviderName => "openai-compatible";
    public int Priority => 0; 

    public bool CanHandle(string provider) 
        => provider != "google" && provider != "bing" && provider != "chrome-ai" && provider != "lingva";

    public async Task<string> ExecuteAsync(
        LlmRequest request, 
        string? customBaseUrl = null, 
        string? customApiKey = null, 
        CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var chunk in StreamExecuteAsync(request, customBaseUrl, customApiKey, ct))
        {
            sb.Append(chunk);
        }
        return sb.ToString();
    }

    public async IAsyncEnumerable<string> StreamExecuteAsync(
        LlmRequest request, 
        string? customBaseUrl = null, 
        string? customApiKey = null, 
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 1. Determine Provider to get config
        string pName = request.ProviderName ?? _configuration["AIProviders:DefaultProvider"] ?? "openai";
        var configSection = _configuration.GetSection($"AIProviders:{pName}");

        string? baseUrl = customBaseUrl ?? configSection["BaseUrl"];
        string? apiKey = customApiKey ?? configSection["ApiKey"];
        string modelId = string.IsNullOrWhiteSpace(request.ModelId) ? (configSection["DefaultModel"] ?? "gpt-4o-mini") : request.ModelId;

        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("OpenAI provider configuration is missing BaseUrl or ApiKey for provider '{Provider}'", pName);
            yield return JsonSerializer.Serialize(new { error = $"Configuration missing for AI Provider '{pName}'. Please check Base URL and API Key." });
            yield break;
        }

        // 2. Initialize compatible OpenAI Client
        var options = new OpenAI.OpenAIClientOptions 
        { 
            Endpoint = new Uri(baseUrl),
            NetworkTimeout = TimeSpan.FromSeconds(300) // Increase timeout to 5 minutes
        };
        var client = new ChatClient(modelId, new ApiKeyCredential(apiKey), options);

        // 3. Map messages
        var chatMessages = request.Messages.Select(m => m.Role.ToLower() switch {
            "system" => (ChatMessage)new SystemChatMessage(m.Content),
            "assistant" => new AssistantChatMessage(m.Content),
            _ => new UserChatMessage(m.Content)
        }).ToList();

        // 4. Configure options
        var maxTokensStr = configSection["MaxTokens"];
        int defaultMaxTokens = 500;
        if (!string.IsNullOrEmpty(maxTokensStr)) int.TryParse(maxTokensStr, out defaultMaxTokens);

        var completionOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = request.MaxTokens ?? defaultMaxTokens,
            Temperature = (float?)(request.Temperature ?? 0.7),
            ResponseFormat = request.ResponseFormatJson ? ChatResponseFormat.CreateJsonObjectFormat() : null
        };

        // 5. Stream results
        IAsyncEnumerator<StreamingChatCompletionUpdate>? enumerator = null;
        Exception? initException = null;

        try
        {
            var updates = client.CompleteChatStreamingAsync(chatMessages, completionOptions, ct);
            enumerator = updates.GetAsyncEnumerator(ct);
        }
        catch (Exception ex)
        {
            initException = ex;
        }

        if (initException != null || enumerator == null)
        {
            _logger.LogError(initException, "Error creating stream with OpenAI SDK.");
            yield return JsonSerializer.Serialize(new { error = $"Request Error: {initException?.Message ?? "Unknown initialization error"}" });
            yield break;
        }

        try
        {
            bool hasMore = true;
            while (hasMore)
            {
                StreamingChatCompletionUpdate? update = null;
                Exception? caughtException = null;

                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        hasMore = false;
                    }
                    else
                    {
                        update = enumerator.Current;
                    }
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                    hasMore = false;
                }

                if (caughtException != null)
                {
                    _logger.LogError(caughtException, "Error during streaming with OpenAI SDK.");
                    yield return JsonSerializer.Serialize(new { error = $"Stream Error: {caughtException.Message}" });
                    break;
                }

                if (update != null)
                {
                    foreach (var part in update.ContentUpdate)
                    {
                        if (!string.IsNullOrEmpty(part.Text))
                        {
                            yield return part.Text;
                        }
                    }
                }
            }
        }
        finally
        {
            if (enumerator != null)
            {
                await enumerator.DisposeAsync();
            }
        }
    }
}
