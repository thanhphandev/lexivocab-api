using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Configuration;

namespace LexiVocab.Infrastructure.Services.Translation.Providers;

public class CloudflareTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _workerUrl;

    public CloudflareTranslationProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["CloudflareAI:ApiKey"] ?? throw new ArgumentNullException("CloudflareAI:ApiKey");
        _workerUrl = configuration["CloudflareAI:WorkerUrl"] ?? throw new ArgumentNullException("CloudflareAI:WorkerUrl");
    }

    public bool CanHandle(string provider) => provider == "cloudflare";

    public async Task<string> TranslateAsync(
        string word, string? context, string providerType, string? modelId, string? from, string? to, 
        string? customBaseUrl, string? customApiKey, string? customModel, 
        CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var chunk in StreamTranslateAsync(word, context, providerType, modelId, from, to, customBaseUrl, customApiKey, customModel, ct))
        {
            sb.Append(chunk);
        }
        return sb.ToString();
    }

    public async IAsyncEnumerable<string> StreamTranslateAsync(
        string word, string? context, string providerType, string? modelId, string? from, string? to, 
        string? customBaseUrl, string? customApiKey, string? customModel, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        var payload = new { word, context, provider = modelId, from, to };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_workerUrl}/translate-stream")
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
            if (line == null) break; 
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
                    contentToYield = data;
                }

                if (contentToYield != null) yield return contentToYield;
            }
            else if (line.StartsWith("{") || line.StartsWith("[")) 
            {
                yield return line;
            }
        }
    }
}
