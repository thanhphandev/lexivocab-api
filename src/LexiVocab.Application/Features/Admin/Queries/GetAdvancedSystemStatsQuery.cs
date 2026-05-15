using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LexiVocab.Application.Features.Admin.Queries;

public record GetAdvancedSystemStatsQuery() : IRequest<Result<AdvancedSystemStatsDto>>;

public class GetAdvancedSystemStatsHandler : IRequestHandler<GetAdvancedSystemStatsQuery, Result<AdvancedSystemStatsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _dateTime;

    public GetAdvancedSystemStatsHandler(IUnitOfWork uow, IDateTimeProvider dateTime)
    {
        _uow = uow;
        _dateTime = dateTime;
    }

    public async Task<Result<AdvancedSystemStatsDto>> Handle(GetAdvancedSystemStatsQuery request, CancellationToken ct)
    {
        var now = _dateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var oneDayAgo = now.AddDays(-1);

        // 1. User Growth Metrics
        var dau = await _uow.Users.CountActiveSinceAsync(oneDayAgo, ct);
        var mau = await _uow.Users.CountActiveSinceAsync(thirtyDaysAgo, ct);

        var newUsersQuery = await _uow.Users.GetNewUsersCountByDateAsync(thirtyDaysAgo, ct);
        var newUsersByDay = newUsersQuery
            .Select(x => new ChartDataPoint(x.Date, x.Count))
            .ToList();

        var userGrowth = new UserGrowthMetrics(dau, mau, newUsersByDay);

        // 2. Financial Metrics
        var activeSubscriptions = await _uow.Subscriptions.GetActiveWithPricingAsync(ct);

        decimal mrr = 0;
        foreach (var sub in activeSubscriptions)
        {
            if (sub.PlanPricing != null && sub.PlanPricing.Price > 0)
            {
                var duration = sub.PlanPricing.DurationDays ?? 30;
                if (duration > 0)
                {
                    mrr += (sub.PlanPricing.Price / duration) * 30;
                }
            }
        }

        var statuses = new[] { SubscriptionStatus.Active, SubscriptionStatus.Cancelled, SubscriptionStatus.Expired };
        
        var totalRelevant = await _uow.Subscriptions.CountSubscriptionsInPeriodAsync(thirtyDaysAgo, statuses, ct);
        var activeCount = await _uow.Subscriptions.CountActiveAsync(ct);
        var cancelledCount = totalRelevant - activeCount;
        if (cancelledCount < 0) cancelledCount = 0;

        var churnRate = totalRelevant > 0 ? (double)cancelledCount / totalRelevant : 0;

        var revenueQuery = await _uow.PaymentTransactions.GetRevenueByDateAsync(thirtyDaysAgo, ct);
        var revenueByDay = revenueQuery
            .Select(x => new ChartDataPoint(x.Date, x.Revenue))
            .ToList();

        var financial = new FinancialMetrics(mrr, Math.Round(churnRate * 100, 2), revenueByDay);

        // 3. Learning Engagement Metrics
        var totalReviewsAllTime = await _uow.ReviewLogs.CountAsync(ct);
        var totalReviewsToday = await _uow.ReviewLogs.CountReviewsSinceAsync(oneDayAgo, ct);
        var averageTimeSpent = await _uow.ReviewLogs.GetAverageTimeSpentSinceAsync(oneDayAgo, ct);
        
        var reviewsInLast30Days = await _uow.ReviewLogs.GetReviewCountByDateAsync(thirtyDaysAgo, ct);
        var reviewsByDay = reviewsInLast30Days
            .Select(x => new ChartDataPoint(x.Date, x.Count))
            .ToList();

        var engagement = new LearningEngagementMetrics(
            totalReviewsToday, 
            Math.Round(averageTimeSpent, 2), 
            totalReviewsAllTime, 
            reviewsByDay);

        var stats = new AdvancedSystemStatsDto(userGrowth, financial, engagement);

        return Result<AdvancedSystemStatsDto>.Success(stats);
    }
}
