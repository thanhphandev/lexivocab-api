namespace LexiVocab.Application.DTOs.Settings;

public record UserSettingsDto(
    bool IsHighlightEnabled,
    string HighlightColor,
    List<string> ExcludedDomains,
    int DailyGoal);

public record UpdateSettingsRequest(
    bool? IsHighlightEnabled,
    string? HighlightColor,
    List<string>? ExcludedDomains,
    int? DailyGoal);
