using System.Net.Http.Json;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Services;

/// <summary>
/// Implementation of IDictionaryService using the Free Dictionary API.
/// </summary>
public class FreeDictionaryClient : IDictionaryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FreeDictionaryClient> _logger;

    public FreeDictionaryClient(HttpClient httpClient, ILogger<FreeDictionaryClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<MasterVocabulary?> FetchWordDefinitionAsync(string word, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(word)) return null;

        try
        {
            // Free Dictionary API: https://dictionaryapi.dev/
            var response = await _httpClient.GetAsync($"https://api.dictionaryapi.dev/api/v2/entries/en/{word.ToLower().Trim()}", ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Word '{Word}' not found in Free Dictionary API.", word);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<List<FreeDictEntry>>(cancellationToken: ct);
            var entry = data?.FirstOrDefault();

            if (entry == null) return null;

            // Map to MasterVocabulary
            // Note: This API returns a list of phonetics, we try to pick relevant ones.
            var phonetic = entry.Phonetics?.FirstOrDefault(p => !string.IsNullOrEmpty(p.Text))?.Text ?? entry.Phonetic;
            var audio = entry.Phonetics?.FirstOrDefault(p => !string.IsNullOrEmpty(p.Audio))?.Audio;

            if (audio != null && audio.StartsWith("//"))
            {
                audio = "https:" + audio;
            }

            return new MasterVocabulary
            {
                Word = entry.Word,
                PartOfSpeech = entry.Meanings?.FirstOrDefault()?.PartOfSpeech,
                PhoneticUk = phonetic,
                PhoneticUs = phonetic, // API doesn't always distinguish UK/US phonetics clearly
                AudioUrl = audio
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching definition for word: {Word}", word);
            return null;
        }
    }

    #region Internal Models
    private class FreeDictEntry
    {
        public string Word { get; set; } = string.Empty;
        public string? Phonetic { get; set; }
        public List<FreeDictPhonetic>? Phonetics { get; set; }
        public List<FreeDictMeaning>? Meanings { get; set; }
    }

    private class FreeDictPhonetic
    {
        public string? Text { get; set; }
        public string? Audio { get; set; }
    }

    private class FreeDictMeaning
    {
        public string? PartOfSpeech { get; set; }
    }
    #endregion
}
