using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Domain.Entities;

namespace LexiVocab.Application.Common.Mappings;

public static class VocabularyMapper
{
    public static VocabularyDto MapToDto(this UserVocabulary v) => new(
        v.Id, v.TagId, v.WordText, v.CustomMeaning, v.ContextSentence, v.SourceUrl,
        v.RepetitionCount, v.EasinessFactor, v.IntervalDays,
        v.NextReviewDate, v.LastReviewedAt, v.IsArchived, v.CreatedAt,
        v.MasterVocabulary?.PhoneticUk, v.MasterVocabulary?.PhoneticUs,
        v.MasterVocabulary?.AudioUrl, v.MasterVocabulary?.PartOfSpeech,
        v.MasterVocabulary?.IsApproved);
}
