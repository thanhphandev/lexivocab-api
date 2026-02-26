using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Analytics;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Analytics;

// ─── Dashboard Overview ─────────────────────────────────────────
public record GetDashboardQuery : IRequest<Result<DashboardDto>>;

public class GetDashboardHandler : IRequestHandler<GetDashboardQuery, Result<DashboardDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetDashboardHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<DashboardDto>> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var (total, active, archived, dueToday) = await _uow.Vocabularies.GetStatsAsync(userId, ct);

        var today = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);

        var (reviewsToday, avgQualityToday, _) = await _uow.ReviewLogs.GetPeriodStatsAsync(
            userId, today, today.AddDays(1), ct);

        var (reviewsThisWeek, avgQualityWeek, _) = await _uow.ReviewLogs.GetPeriodStatsAsync(
            userId, weekStart, today.AddDays(1), ct);

        var currentStreak = await _uow.ReviewLogs.GetCurrentStreakAsync(userId, ct);

        // Count total active study days from heatmap
        var yearStart = new DateOnly(today.Year, 1, 1);
        var yearEnd = DateOnly.FromDateTime(today);
        var heatmap = await _uow.ReviewLogs.GetHeatmapDataAsync(userId, yearStart, yearEnd, ct);
        var totalStudyDays = heatmap.Count(h => h.Count > 0);

        return Result<DashboardDto>.Success(new DashboardDto(
            new VocabularyOverviewDto(total, active, archived, dueToday),
            new ReviewOverviewDto(reviewsToday, reviewsThisWeek, avgQualityWeek),
            currentStreak,
            totalStudyDays));
    }
}

// ─── Heatmap Data ───────────────────────────────────────────────
public record GetHeatmapQuery(int? Year = null) : IRequest<Result<HeatmapDataDto>>;

public class GetHeatmapHandler : IRequestHandler<GetHeatmapQuery, Result<HeatmapDataDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetHeatmapHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<HeatmapDataDto>> Handle(GetHeatmapQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var year = request.Year ?? DateTime.UtcNow.Year;
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);

        var data = await _uow.ReviewLogs.GetHeatmapDataAsync(userId, from, to, ct);
        var entries = data.Select(d => new HeatmapEntryDto(d.Date, d.Count)).ToList();

        return Result<HeatmapDataDto>.Success(new HeatmapDataDto(entries, year));
    }
}

// ─── Streak ─────────────────────────────────────────────────────
public record GetStreakQuery : IRequest<Result<StreakDto>>;

public class GetStreakHandler : IRequestHandler<GetStreakQuery, Result<StreakDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetStreakHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<StreakDto>> Handle(GetStreakQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var currentStreak = await _uow.ReviewLogs.GetCurrentStreakAsync(userId, ct);
        var longestStreak = await _uow.ReviewLogs.GetLongestStreakAsync(userId, ct);

        // Determine last active date from heatmap data
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var heatmap = await _uow.ReviewLogs.GetHeatmapDataAsync(
            userId,
            today.AddDays(-365),
            today,
            ct);

        var lastActiveDate = heatmap
            .Where(h => h.Count > 0)
            .Select(h => h.Date)
            .OrderByDescending(d => d)
            .FirstOrDefault();

        return Result<StreakDto>.Success(new StreakDto(
            currentStreak,
            longestStreak,
            lastActiveDate == default ? null : lastActiveDate));
    }
}
