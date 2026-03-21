using System;
using System.Collections.Generic;

namespace LexiVocab.Infrastructure.Services.Translation.Providers;

public static class LanguageMapper
{
    private static readonly Dictionary<string, string> _languages = new(StringComparer.OrdinalIgnoreCase)
    {
        { "en", "English" }, { "vi", "Tiếng Việt" }, { "es", "Español" }, { "fr", "Français" },
        { "ja", "日本語" }, { "ko", "한국어" }, { "zh", "中文 (Simplified)" }, { "zh-hant", "中文 (Traditional)" },
        { "de", "Deutsch" }, { "it", "Italiano" }, { "ru", "Russian" }, { "pt", "Portuguese" },
        { "ar", "العربية" }, { "hi", "Hindi" }, { "id", "Bahasa Indonesia" }, { "th", "Thai" },
        { "tr", "Türkçe" }, { "nl", "Nederlands" }, { "pl", "Polski" }, { "sv", "Svenska" },
        { "da", "Dansk" }, { "no", "Norsk" }, { "fi", "Suomi" }, { "el", "Greek" },
        { "cs", "Čeština" }, { "hu", "Magyar" }, { "ro", "Română" }, { "uk", "Ukrainian" },
        { "ms", "Bahasa Melayu" }, { "tl", "Filipino" }, { "he", "Hebrew" }, { "fa", "Persian" },
        { "bn", "Bengali" }, { "pa", "Punjabi" }, { "mr", "Marathi" }, { "ta", "Tamil" },
        { "te", "Telugu" }, { "ur", "Urdu" }, { "sw", "Swahili" }, { "am", "Amharic" },
        { "az", "Azerbaijani" }, { "uz", "Uzbek" }, { "kk", "Kazakh" }, { "ka", "Georgian" },
        { "sk", "Slovak" }, { "hr", "Croatian" }, { "bg", "Bulgarian" }, { "sr", "Serbian" },
        { "sl", "Slovenian" }, { "et", "Estonian" }, { "lv", "Latvian" }, { "lt", "Lithuanian" },
        { "is", "Icelandic" }, { "af", "Afrikaans" }, { "my", "Burmese" }, { "km", "Khmer" },
        { "lo", "Lao" }, { "sq", "Albanian" }, { "hy", "Armenian" }, { "eu", "Basque" },
        { "be", "Belarusian" }, { "bs", "Bosnian" }, { "ca", "Catalan" }, { "gl", "Galician" },
        { "gu", "Gujarati" }, { "kn", "Kannada" }, { "ml", "Malayalam" }, { "mn", "Mongolian" },
        { "ne", "Nepali" }, { "si", "Sinhala" }, { "tg", "Tajik" }, { "tk", "Turkmen" },
        { "cy", "Welsh" }, { "yo", "Yoruba" }, { "zu", "Zulu" }
    };

    /// <summary>
    /// Maps a language code (e.g. "vi") to its full human-readable name ("Tiếng Việt").
    /// </summary>
    public static string GetName(string? code, bool isSource = false)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return isSource ? "the source language" : "Vietnamese";

        return _languages.TryGetValue(code, out var name) ? name : code;
    }
}
