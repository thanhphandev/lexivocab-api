using LexiVocab.Domain.Enums;

namespace LexiVocab.Application.DTOs.Review;

public record SubmitReviewRequest(
    Guid UserVocabularyId,
    QualityScore QualityScore,
    int? TimeSpentMs);

public record ReviewSessionDto(
    IReadOnlyList<ReviewCardDto> Cards,
    int TotalDue);

public record ReviewCardDto(
    Guid VocabularyId,
    string WordText,
    string? CustomMeaning,
    string? ContextSentence,
    string? PhoneticUs,
    string? AudioUrl,
    int RepetitionCount,
    double EasinessFactor);

public record ReviewResultDto(
    Guid VocabularyId,
    int NewRepetitionCount,
    double NewEasinessFactor,
    int NewIntervalDays,
    DateTime NextReviewDate);

public record ReviewHistoryDto(
    Guid Id,
    Guid UserVocabularyId,
    string WordText,
    QualityScore QualityScore,
    int? TimeSpentMs,
    DateTime ReviewedAt);
