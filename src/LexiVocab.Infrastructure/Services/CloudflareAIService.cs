using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services;

public class CloudflareAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudflareAIService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _apiKey;
    private readonly string _workerUrl;

    public CloudflareAIService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CloudflareAIService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _apiKey = configuration["AIProviders:cloudflare:ApiKey"] ?? throw new ArgumentNullException("AIProviders:cloudflare:ApiKey");
        _workerUrl = configuration["AIProviders:cloudflare:BaseUrl"] ?? throw new ArgumentNullException("AIProviders:cloudflare:BaseUrl");
    }

    public async Task<string?> GetRelatedWordsAsync(string word, string? targetLanguage = null, string? userLanguage = null, CancellationToken ct = default)
    {
        var mappedTargetLanguage = LexiVocab.Infrastructure.Services.Translation.Providers.LanguageMapper.GetName(targetLanguage, false);
        var mappedUserLanguage = LexiVocab.Infrastructure.Services.Translation.Providers.LanguageMapper.GetName(userLanguage, false);
        return await CallWorkerAsync("suggest-related", new { word, targetLanguage = mappedTargetLanguage, userLanguage = mappedUserLanguage }, ct);
    }

    public async Task<string?> GenerateQuizAsync(string word, string? targetLanguage = null, string? userLanguage = null, CancellationToken ct = default)
    {
        var mappedTargetLanguage = LexiVocab.Infrastructure.Services.Translation.Providers.LanguageMapper.GetName(targetLanguage, false);
        var mappedUserLanguage = LexiVocab.Infrastructure.Services.Translation.Providers.LanguageMapper.GetName(userLanguage, false);
        return await CallWorkerAsync("generate-quiz", new { word, targetLanguage = mappedTargetLanguage, userLanguage = mappedUserLanguage }, ct);
    }

    public async Task<string?> GenerateFillBlankAsync(string word, string? targetLanguage = null, string? userLanguage = null, CancellationToken ct = default)
    {
        var mappedTargetLanguage = LexiVocab.Infrastructure.Services.Translation.Providers.LanguageMapper.GetName(targetLanguage, false);
        var mappedUserLanguage = LexiVocab.Infrastructure.Services.Translation.Providers.LanguageMapper.GetName(userLanguage, false);
        return await CallWorkerAsync("generate-fill-blank", new { word, targetLanguage = mappedTargetLanguage, userLanguage = mappedUserLanguage }, ct);
    }



    public async IAsyncEnumerable<string> StreamExplainUsageAsync(string word, string? context = null, bool asJson = false, string? targetLanguage = null, string? userLanguage = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var mappedTargetLanguage = LexiVocab.Infrastructure.Services.Translation.Providers.LanguageMapper.GetName(targetLanguage, false);
        var mappedUserLanguage = LexiVocab.Infrastructure.Services.Translation.Providers.LanguageMapper.GetName(userLanguage, false);

        var payload = new 
        { 
            word, 
            context, 
            format = asJson ? "json" : null,
            targetLanguage = mappedTargetLanguage,
            userLanguage = mappedUserLanguage
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_workerUrl}/explain-usage-stream")
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break; // End of stream
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("data: "))
            {
                var data = line["data: ".Length..];
                if (data == "[DONE]") break;

                string? contentToYield = null;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    if (doc.RootElement.TryGetProperty("response", out var text))
                    {
                        contentToYield = text.GetString();
                    }
                    else if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var delta = choices[0].GetProperty("delta");
                        if (delta.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String)
                        {
                            contentToYield = contentElement.GetString();
                        }
                    }
                }
                catch
                {
                    // If parsing fails, treat it as raw text
                    contentToYield = data;
                }

                if (contentToYield != null)
                {
                    yield return contentToYield;
                }
            }
        }
    }

    private async Task<string?> CallWorkerAsync(string endpoint, object payload, CancellationToken ct)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_workerUrl}/{endpoint}")
            {
                Content = JsonContent.Create(payload)
            };
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(httpRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cloudflare AI '{Endpoint}' failed: {StatusCode}", endpoint, response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Cloudflare AI endpoint '{Endpoint}'", endpoint);
            return null;
        }
    }

    private class CloudflareAIResponse
    {
        [JsonPropertyName("partOfSpeech")]
        public string? PartOfSpeech { get; set; }

        [JsonPropertyName("phoneticUk")]
        public string? PhoneticUk { get; set; }

        [JsonPropertyName("phoneticUs")]
        public string? PhoneticUs { get; set; }

        [JsonPropertyName("definition")]
        public string? Definition { get; set; }

        [JsonPropertyName("exampleSentence")]
        public string? ExampleSentence { get; set; }

        [JsonPropertyName("cefrLevel")]
        public string? CefrLevel { get; set; }
    }
}
