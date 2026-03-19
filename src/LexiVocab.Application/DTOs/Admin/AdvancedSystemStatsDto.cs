using System;
using System.Collections.Generic;

namespace LexiVocab.Application.DTOs.Admin;

public record AdvancedSystemStatsDto(
    UserGrowthMetrics UserGrowth,
    FinancialMetrics Financial,
    LearningEngagementMetrics Engagement
);

public record UserGrowthMetrics(
    int DailyActiveUsers,
    int MonthlyActiveUsers,
    IReadOnlyList<ChartDataPoint> NewUsersByDay
);

public record FinancialMetrics(
    decimal MonthlyRecurringRevenue,
    double ChurnRate,
    IReadOnlyList<ChartDataPoint> RevenueByDay 
);

public record LearningEngagementMetrics(
    int TotalReviewsToday,
    double AverageTimeSpentPerReviewMs,
    int TotalReviews,
    IReadOnlyList<ChartDataPoint> ReviewsByDay
);

public record ChartDataPoint(string Label, decimal Value);
