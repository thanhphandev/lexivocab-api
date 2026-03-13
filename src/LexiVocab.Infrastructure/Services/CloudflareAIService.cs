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
    private readonly string _apiKey;
    private readonly string _workerUrl;

    public CloudflareAIService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CloudflareAIService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["CloudflareAI:ApiKey"] ?? throw new ArgumentNullException("CloudflareAI:ApiKey");
        _workerUrl = configuration["CloudflareAI:WorkerUrl"] ?? throw new ArgumentNullException("CloudflareAI:WorkerUrl");
    }

    public async Task<MasterVocabulary?> EnrichWordAsync(string word, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(word)) return null;

        try
        {
            var json = await CallWorkerAsync("enrich-word", new { word }, ct);
            if (string.IsNullOrEmpty(json)) return null;

            var aiResult = JsonSerializer.Deserialize<CloudflareAIResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (aiResult == null) return null;

            return new MasterVocabulary
            {
                Word = word.ToLower().Trim(),
                PartOfSpeech = aiResult.PartOfSpeech,
                PhoneticUk = aiResult.PhoneticUk,
                PhoneticUs = aiResult.PhoneticUs,
                IsFetchFailed = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ AI enrichment failed for '{Word}'", word);
            return null;
        }
    }

    public async Task<string?> ExplainUsageAsync(string word, string? context = null, CancellationToken ct = default)
    {
        return await CallWorkerAsync("explain-usage", new { word, context }, ct);
    }

    public async Task<string?> GetRelatedWordsAsync(string word, CancellationToken ct = default)
    {
        return await CallWorkerAsync("suggest-related", new { word }, ct);
    }

    public async Task<string?> GenerateQuizAsync(string word, CancellationToken ct = default)
    {
        return await CallWorkerAsync("generate-quiz", new { word }, ct);
    }

    public async Task<string?> GenerateMnemonicAsync(string word, string meaning, CancellationToken ct = default)
    {
        return await CallWorkerAsync("generate-mnemonic", new { word, meaning }, ct);
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
    }
}
