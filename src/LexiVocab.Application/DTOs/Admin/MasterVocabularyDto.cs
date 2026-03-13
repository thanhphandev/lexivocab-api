namespace LexiVocab.Application.DTOs.Admin;

public record MasterVocabularyDto(
    Guid Id,
    string Word,
    string? PartOfSpeech,
    string? PhoneticUk,
    string? PhoneticUs,
    string? AudioUrl,
    int? PopularityRank,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
