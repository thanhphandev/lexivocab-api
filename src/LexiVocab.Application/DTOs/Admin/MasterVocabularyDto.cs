namespace LexiVocab.Application.DTOs.Admin;

public record MasterVocabularyDto(
    Guid Id,
    string Word,
    string? PartOfSpeech,
    string? PhoneticUk,
    string? PhoneticUs,
    string? AudioUrl,
    int? PopularityRank,
    string? Meaning,
    string? CefrLevel,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    // Alias for frontend compatibility with UserVocabulary components
    public string WordText => Word;
}
