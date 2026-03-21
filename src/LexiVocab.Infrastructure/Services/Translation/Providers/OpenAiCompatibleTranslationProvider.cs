using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services.Translation.Providers;

public class OpenAiCompatibleTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiCompatibleTranslationProvider> _logger;

    public OpenAiCompatibleTranslationProvider(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAiCompatibleTranslationProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public bool CanHandle(string provider) => provider != "cloudflare" && provider != "google" && provider != "lingva" && provider != "bing";

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
        string? baseUrl = customBaseUrl;
        string? apiKey = customApiKey;
        string? model = customModel ?? modelId;

        if (string.IsNullOrEmpty(baseUrl))
        {
            var configSection = _configuration.GetSection($"AIProviders:{providerType}");
            baseUrl = configSection?["BaseUrl"];
            apiKey = configSection?["ApiKey"];
        }

        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("OpenAI provider '{Provider}' is missing BaseUrl or ApiKey", providerType);
            yield return JsonSerializer.Serialize(new { error = $"Configuration missing for Custom Provider ({(string.IsNullOrWhiteSpace(modelId) ? providerType : modelId)}). Please check Base URL and API Key." });
            yield break;
        }

        string targetLang = LanguageMapper.GetName(to, false);
        string sourceLang = LanguageMapper.GetName(from, true);

        string systemContent = $"You are a strict translation API. Your response must be EXACTLY and ONLY a raw JSON object. NO markdown formatting. NO code blocks (do not use ```json). NO conversational text. Format strictly as:\n{{\n  \"word\": \"translated/root form\",\n  \"meaning\": \"ONLY the short, direct translation in {targetLang}. NO explanations.\",\n  \"phonetic\": \"IPA transcription\",\n  \"context\": \"translated/simplified context sentence\"\n}}";

        string userContent = $"Translate the word \"{word}\" from {sourceLang} to {targetLang}{(string.IsNullOrWhiteSpace(context) ? "" : $" using the following context: \"{context}\"")}.\n\nRemember: Output NOTHING but the raw JSON object starting with {{.";

        var payload = new
        {
            model = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model,
            messages = new[]
            {
                new { role = "system", content = systemContent },
                new { role = "user", content = userContent }
            },
            max_tokens = 300,
            stream = true,
            response_format = new { type = "json_object" }
        };

        var requestUri = baseUrl.EndsWith("/chat/completions") ? baseUrl : $"{baseUrl.TrimEnd('/')}/chat/completions";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to call {Provider}: {Status} - {Body}", providerType, response.StatusCode, errorBody);
            
            string errorMessage = $"HTTP {response.StatusCode} from {providerType}";
            try {
                using var doc = JsonDocument.Parse(errorBody);
                if (doc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    if (errorElement.ValueKind == JsonValueKind.String) {
                         errorMessage += ": " + errorElement.GetString();
                    } else if (errorElement.ValueKind == JsonValueKind.Object && errorElement.TryGetProperty("message", out var msgElement)) {
                         errorMessage += ": " + msgElement.GetString();
                    }
                }
            } catch {
                errorMessage += $" | {errorBody.Trim().Substring(0, Math.Min(errorBody.Trim().Length, 150))}";
            }

            yield return JsonSerializer.Serialize(new { error = errorMessage });
            yield break;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType == "application/json")
        {
            var fullJson = await response.Content.ReadAsStringAsync(ct);
            string? parsedError = null;
            string? parsedContent = null;
            try {
                using var doc = JsonDocument.Parse(fullJson);
                if (doc.RootElement.TryGetProperty("error", out var errObj)) {
                    parsedError = $"Provider Error: {errObj.GetRawText()}";
                }
                else if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var msg = choices[0].GetProperty("message");
                    if (msg.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String) {
                        parsedContent = contentElement.GetString() ?? "";
                    }
                }
            } catch {}
            
            if (parsedError != null)
            {
                yield return JsonSerializer.Serialize(new { error = parsedError });
                yield break;
            }
            if (parsedContent != null)
            {
                yield return parsedContent;
                yield break;
            }
            
            yield return JsonSerializer.Serialize(new { error = $"Unexpected JSON response from provider: {fullJson.Trim().Substring(0, Math.Min(fullJson.Trim().Length, 150))}" });
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        bool hasYieldedAny = false;
        var unparsedLines = new List<string>();

        // Lưới lọc thông minh cho JSON Stream (JSON State Machine Tracker)
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
                        errorToYield = $"Stream Error: {streamErr.GetRawText()}";
                    }
                    else if (doc.RootElement.TryGetProperty("code", out var codeErr) && doc.RootElement.TryGetProperty("message", out var msgErr))
                    {
                        errorToYield = $"Provider Error: [{codeErr.GetString()}] {msgErr.GetString()}";
                    }
                    else if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var delta = choices[0].GetProperty("delta");
                        if (delta.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String)
                        {
                            string rawContent = contentElement.GetString() ?? "";
                            if (!hasFinishedJson)
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
                    else
                    {
                        unparsedLines.Add(line);
                    }
                }
                catch {
                    unparsedLines.Add(line);
                }
                
                if (errorToYield != null)
                {
                    hasYieldedAny = true;
                    yield return JsonSerializer.Serialize(new { error = errorToYield });
                    yield break;
                }

                if (!string.IsNullOrEmpty(contentToYield))
                {
                    hasYieldedAny = true;
                    yield return contentToYield;
                }
            }
            else if (line.TrimStart().StartsWith("{") && line.Contains("\"error\""))
            {
                string? rawErrorToYield = null;
                try {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("error", out var rawErr))
                    {
                        rawErrorToYield = $"Provider Error: {rawErr.GetRawText()}";
                    }
                } catch {
                    unparsedLines.Add(line);
                }
                
                if (rawErrorToYield != null)
                {
                    hasYieldedAny = true;
                    yield return JsonSerializer.Serialize(new { error = rawErrorToYield });
                    yield break;
                }
            }
            else
            {
                unparsedLines.Add(line);
            }
        }
        
        if (!hasYieldedAny && unparsedLines.Count > 0)
        {
            string fallbackError = string.Join(" ", unparsedLines);
            yield return JsonSerializer.Serialize(new { error = $"Unrecognized stream response: {fallbackError.Substring(0, Math.Min(fallbackError.Length, 200))}" });
        }
    }
}
