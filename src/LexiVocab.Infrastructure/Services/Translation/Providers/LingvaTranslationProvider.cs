using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;

namespace LexiVocab.Infrastructure.Services.Translation.Providers;

public class LingvaTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;

    public LingvaTranslationProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool CanHandle(string provider) => provider == "lingva";

    public async Task<string> TranslateAsync(
        string word, string? context, string providerType, string? modelId, string? from, string? to, 
        string? customBaseUrl, string? customApiKey, string? customModel, 
        CancellationToken ct)
    {
        string sourceLang = string.IsNullOrWhiteSpace(from) || from == "auto" ? "auto" : from;
        string targetLang = string.IsNullOrWhiteSpace(to) || to == "auto" ? "vi" : to;

        // string textToTranslate = string.IsNullOrEmpty(context) ? word : $"{word} ({context})";
        var url = $"https://lingva.ml/api/v1/{sourceLang}/{targetLang}/{Uri.EscapeDataString(word   )}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorObj = new { error = "Lingva Translate failed" };
            return JsonSerializer.Serialize(errorObj);
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        string translation = "";
        bool parseError = false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("translation", out var transElement))
            {
                translation = transElement.GetString() ?? "";
            }
        }
        catch
        {
            parseError = true;
        }

        if (parseError || string.IsNullOrWhiteSpace(translation))
        {
            var errorObj = new { error = "Lingva Translate parse failed or returned empty" };
            return JsonSerializer.Serialize(errorObj);
        }

        var resultObj = new { word = word, meaning = translation };
        return JsonSerializer.Serialize(resultObj);
    }

    public async IAsyncEnumerable<string> StreamTranslateAsync(
        string word, string? context, string providerType, string? modelId, string? from, string? to, 
        string? customBaseUrl, string? customApiKey, string? customModel, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        yield return await TranslateAsync(word, context, providerType, modelId, from, to, customBaseUrl, customApiKey, customModel, ct);
    }
}
