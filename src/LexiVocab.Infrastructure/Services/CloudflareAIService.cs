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

    public async IAsyncEnumerable<string> StreamExplainUsageAsync(string word, string? context = null, bool asJson = false, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new 
        { 
            word, 
            context, 
            format = asJson ? "json" : null 
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
    }
}
