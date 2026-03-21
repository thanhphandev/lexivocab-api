using System.Text.Json.Serialization;

namespace LexiVocab.Application.DTOs.Games;

public record GameHubDataDto(
    int TotalXP,
    int CurrentLevel,
    int CurrentStreak,
    int Hearts,
    List<GlobalDeckDto> AvailableGlobalDecks
);

public record GlobalDeckDto(
    Guid Id,
    string Title,
    string Description,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ImageUrl,
    int DifficultyLevel,
    string TargetLanguage
);

public record GameSessionRequestDto(
    string Mode, // "Personal" or "Global"
    Guid? DeckId,
    int QuestionCount = 10
);

public record GameSessionResponseDto(
    Guid SessionId,
    List<GameQuestionDto> Questions
);

public record GameQuestionDto(
    string Type, // "MultipleChoice", "FillBlank", "Pronunciation"
    string Word,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Context,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<string>? Options,
    int CorrectIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Explanation
);

public record CompleteGameSessionCommandDto(
    Guid SessionId,
    int XPEarned,
    int MistakesMade,
    List<string> ReviewWords // Words to add to vocab or reschedule
);

public record GameSessionResultDto(
    bool Success,
    int NewTotalXP,
    int NewLevel,
    int HeartsRemaining
);
