using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Domain.Models.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services.AI.Providers;

public class OpenAiCompatibleLLMProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiCompatibleLLMProvider> _logger;

    public OpenAiCompatibleLLMProvider(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ILogger<OpenAiCompatibleLLMProvider> logger)
    {
        _httpClient = httpClient;
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
        string? baseUrl = customBaseUrl;
        string? apiKey = customApiKey;

        if (string.IsNullOrEmpty(baseUrl))
        {
            string pName = request.ProviderName ?? "openrouter";
            var configSection = _configuration.GetSection($"AIProviders:{pName}");
            baseUrl = configSection?["BaseUrl"];
            apiKey = configSection?["ApiKey"];
        }

        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("OpenAI provider is missing BaseUrl or ApiKey");
            yield return JsonSerializer.Serialize(new { error = "Configuration missing for Custom Provider. Please check Base URL and API Key." });
            yield break;
        }

        var payload = new
        {
            model = string.IsNullOrWhiteSpace(request.ModelId) ? "gpt-4o-mini" : request.ModelId,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            max_tokens = request.MaxTokens ?? 500,
            temperature = request.Temperature ?? 0.7,
            stream = true,
            response_format = request.ResponseFormatJson ? new { type = "json_object" } : null
        };

        var requestUri = baseUrl.EndsWith("/chat/completions") ? baseUrl : $"{baseUrl.TrimEnd('/')}/chat/completions";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            yield return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}: {errorBody}" });
            yield break;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType == "application/json")
        {
            var fullJson = await response.Content.ReadAsStringAsync(ct);
            string? parsedContent = null;
            try
            {
                using var doc = JsonDocument.Parse(fullJson);
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    parsedContent = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                }
            }
            catch { }
            
            if (parsedContent != null)
            {
                yield return parsedContent;
                yield break;
            }
            
            yield return fullJson;
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        int braceDepth = 0;
        bool hasStartedJson = false;
        bool hasFinishedJson = false;
        bool inString = false;
        bool escapeNext = false;

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("data: "))
            {
                var data = line["data: ".Length..].Trim();
                if (data == "[DONE]") break;

                string? contentToYield = null;
                string? errorToYield = null;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    if (doc.RootElement.TryGetProperty("error", out var streamErr))
                    {
                        errorToYield = JsonSerializer.Serialize(new { error = $"Stream Error: {streamErr.GetRawText()}" });
                    }
                    else if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var delta = choices[0].GetProperty("delta");
                        if (delta.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String)
                        {
                            string rawContent = contentElement.GetString() ?? "";
                            if (!request.ResponseFormatJson)
                            {
                                contentToYield = rawContent;
                            }
                            else if (!hasFinishedJson)
                            {
                                string filteredContent = "";
                                foreach (char c in rawContent)
                                {
                                    if (!hasStartedJson)
                                    {
                                        if (c == '{')
                                        {
                                            hasStartedJson = true;
                                            braceDepth = 1;
                                            filteredContent += c;
                                        }
                                    }
                                    else
                                    {
                                        filteredContent += c;

                                        if (escapeNext) escapeNext = false;
                                        else if (c == '\\') escapeNext = true;
                                        else if (c == '"') inString = !inString;
                                        else if (!inString)
                                        {
                                            if (c == '{') braceDepth++;
                                            else if (c == '}') braceDepth--;
                                        }

                                        if (braceDepth == 0 && !inString && hasStartedJson)
                                        {
                                            hasFinishedJson = true;
                                            break;
                                        }
                                    }
                                }
                                
                                if (!string.IsNullOrEmpty(filteredContent))
                                {
                                    contentToYield = filteredContent;
                                }
                            }
                        }
                    }
                }
                catch { }

                if (errorToYield != null)
                {
                    yield return errorToYield;
                    yield break;
                }

                if (!string.IsNullOrEmpty(contentToYield))
                {
                    yield return contentToYield;
                }
            }
        }
    }
}
