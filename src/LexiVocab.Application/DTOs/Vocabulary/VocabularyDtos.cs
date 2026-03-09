namespace LexiVocab.Application.DTOs.Vocabulary;

public record VocabularyDto(
    Guid Id,
    Guid? TagId,
    string WordText,
    string? CustomMeaning,
    string? ContextSentence,
    string? SourceUrl,
    int RepetitionCount,
    double EasinessFactor,
    int IntervalDays,
    DateTime NextReviewDate,
    DateTime? LastReviewedAt,
    bool IsArchived,
    DateTime CreatedAt,
    // From MasterVocabulary (if linked)
    string? PhoneticUk,
    string? PhoneticUs,
    string? AudioUrl,
    string? PartOfSpeech);

public record CreateVocabularyRequest(
    string WordText,
    string? CustomMeaning,
    string? ContextSentence,
    string? SourceUrl,
    Guid? TagId = null);

public record UpdateVocabularyRequest(
    string? CustomMeaning,
    string? ContextSentence);

public record UpdateVocabularyTagRequest(
    Guid? TagId);

public record BatchImportRequest(
    List<CreateVocabularyRequest> Words);

public record VocabularyStatsDto(
    int Total,
    int Active,
    int Archived,
    int DueToday);
