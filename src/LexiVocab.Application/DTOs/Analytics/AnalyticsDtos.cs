using LexiVocab.Application.DTOs.Vocabulary;
using System.Collections.Generic;

namespace LexiVocab.Application.DTOs.Analytics;

public record DashboardDto(
    VocabularyOverviewDto Vocabulary,
    ReviewOverviewDto Reviews,
    int CurrentStreak,
    int TotalStudyDays);

public record VocabularyOverviewDto(
    int TotalWords,
    int ActiveWords,
    int MasteredWords,
    int DueToday,
    IReadOnlyList<VocabularyDto> RecentVocabulary);

public record ReviewOverviewDto(
    int TotalReviewsToday,
    int TotalReviewsThisWeek,
    double AverageQualityScore);

public record HeatmapDataDto(
    IReadOnlyList<HeatmapEntryDto> Entries,
    int Year);

public record HeatmapEntryDto(
    DateOnly Date,
    int Count);

public record StreakDto(
    int CurrentStreak,
    int LongestStreak,
    DateOnly? LastActiveDate);

public record DueCountDto(int DueCount);
