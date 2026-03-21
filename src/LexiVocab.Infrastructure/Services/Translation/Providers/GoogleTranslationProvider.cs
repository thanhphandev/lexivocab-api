using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;

namespace LexiVocab.Infrastructure.Services.Translation.Providers;

public class GoogleTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;

    public GoogleTranslationProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool CanHandle(string provider) => provider == "google";

    public async Task<string> TranslateAsync(
        string word, string? context, string providerType, string? modelId, string? from, string? to, 
        string? customBaseUrl, string? customApiKey, string? customModel, 
        CancellationToken ct)
    {
        string sourceLang = string.IsNullOrWhiteSpace(from) || from == "auto" ? "auto" : from;
        string targetLang = string.IsNullOrWhiteSpace(to) || to == "auto" ? "vi" : to;

        // string textToTranslate = string.IsNullOrEmpty(context) ? word : $"{word} ({context})";
        var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sourceLang}&tl={targetLang}&dt=t&q={Uri.EscapeDataString(word)}";

        HttpResponseMessage? response = null;
        int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            response = await _httpClient.GetAsync(url, ct);
            if (response.IsSuccessStatusCode) break;
            
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)), ct);
                continue;
            }
            break;
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            var errorObj = new { error = "Google Translate failed", status = response?.StatusCode.ToString() };
            return JsonSerializer.Serialize(errorObj);
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        
        string translation = "";
        bool parseError = false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var lines = root[0]; 
                if (lines.ValueKind == JsonValueKind.Array)
                {
                    foreach (var line in lines.EnumerateArray())
                    {
                        if (line.GetArrayLength() > 0 && line[0].ValueKind == JsonValueKind.String)
                        {
                            translation += line[0].GetString();
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            parseError = true;
        }

        if (parseError || string.IsNullOrWhiteSpace(translation))
        {
            var errorObj = new { error = "Google Translate parse failed or returned empty" };
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
