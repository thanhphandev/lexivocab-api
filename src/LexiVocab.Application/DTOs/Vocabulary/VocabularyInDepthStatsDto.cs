using System.Collections.Generic;

namespace LexiVocab.Application.DTOs.Vocabulary;

public record VocabularyInDepthStatsDto(
    double RetentionRate,
    double LearningProgress,
    int WordsLearnedThisWeek,
    IReadOnlyList<string> MostDifficultWords,
    IReadOnlyDictionary<string, int> CefrSpread);
