namespace LexiVocab.Application.DTOs.Settings;

public record UserSettingsDto(
    bool IsHighlightEnabled,
    string HighlightColor,
    List<string> ExcludedDomains,
    int DailyGoal,
    string PreferencesJson);

public record UpdateSettingsRequest(
    bool? IsHighlightEnabled,
    string? HighlightColor,
    List<string>? ExcludedDomains,
    int? DailyGoal,
    string? PreferencesJson);
