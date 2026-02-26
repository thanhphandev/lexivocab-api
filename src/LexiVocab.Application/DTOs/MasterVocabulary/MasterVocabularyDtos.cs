namespace LexiVocab.Application.DTOs.MasterVocabulary;

public record MasterVocabularyDto(
    Guid Id,
    string Word,
    string? PartOfSpeech,
    string? PhoneticUk,
    string? PhoneticUs,
    string? AudioUrl,
    int? PopularityRank);

public record MasterVocabularySearchResultDto(
    string Word,
    string? PartOfSpeech,
    string? PhoneticUs,
    int? PopularityRank);
