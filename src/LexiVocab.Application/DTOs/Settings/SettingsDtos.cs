namespace LexiVocab.Application.DTOs.Settings;

public record UserSettingsDto(
    bool IsHighlightEnabled,
    string HighlightColor,
    List<string> ExcludedDomains,
    int DailyGoal,
    int DailyNewCardLimit,
    int DailyReviewLimit,
    string PreferencesJson,
    string TargetLanguage,
    string NativeLanguage,
    string CustomLlmsJson,
    string DefaultTranslator);

public record UpdateSettingsRequest(
    bool? IsHighlightEnabled,
    string? HighlightColor,
    List<string>? ExcludedDomains,
    int? DailyGoal,
    int? DailyNewCardLimit,
    int? DailyReviewLimit,
    string? PreferencesJson,
    string? TargetLanguage,
    string? NativeLanguage,
    string? CustomLlmsJson,
    string? DefaultTranslator);
