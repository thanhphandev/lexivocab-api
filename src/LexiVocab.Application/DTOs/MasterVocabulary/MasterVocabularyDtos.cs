namespace LexiVocab.Application.DTOs.MasterVocabulary;

public record MasterVocabularyDto(
    Guid Id,
    string Word,
    string? PartOfSpeech,
    string? PhoneticUk,
    string? PhoneticUs,
    string? AudioUrl,
    int? PopularityRank,
    string? Meaning = null,
    string? CefrLevel = null)
{
    public string WordText => Word;
}

public record MasterVocabularySearchResultDto(
    string Word,
    string? PartOfSpeech,
    string? PhoneticUs,
    int? PopularityRank,
    string? Meaning = null,
    string? CefrLevel = null);
