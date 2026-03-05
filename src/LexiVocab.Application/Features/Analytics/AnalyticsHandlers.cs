using System.Text.Json;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Analytics;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Analytics;

// ─── Dashboard Overview ─────────────────────────────────────────
public record GetDashboardQuery : IRequest<Result<DashboardDto>>;

public class GetDashboardHandler : IRequestHandler<GetDashboardQuery, Result<DashboardDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public GetDashboardHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result<DashboardDto>> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        // Use the same version buster as vocabulary for synchronization
        var version = await _cache.GetStringAsync($"vocab-v:{userId}", ct) ?? "0";
        var cacheKey = $"analytics-dashboard:{userId}:v{version}";

        var cachedData = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var result = JsonSerializer.Deserialize<DashboardDto>(cachedData);
            if (result != null) return Result<DashboardDto>.Success(result);
        }

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

        var dashboardDto = new DashboardDto(
            new VocabularyOverviewDto(total, active, archived, dueToday),
            new ReviewOverviewDto(reviewsToday, reviewsThisWeek, avgQualityWeek),
            currentStreak,
            totalStudyDays);

        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dashboardDto), options, ct);

        return Result<DashboardDto>.Success(dashboardDto);
    }
}

// ─── Heatmap Data ───────────────────────────────────────────────
public record GetHeatmapQuery(int? Year = null) : IRequest<Result<HeatmapDataDto>>;

public class GetHeatmapHandler : IRequestHandler<GetHeatmapQuery, Result<HeatmapDataDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public GetHeatmapHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result<HeatmapDataDto>> Handle(GetHeatmapQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var year = request.Year ?? DateTime.UtcNow.Year;
        
        var version = await _cache.GetStringAsync($"vocab-v:{userId}", ct) ?? "0";
        var cacheKey = $"analytics-heatmap:{userId}:{year}:v{version}";

        var cachedData = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var result = JsonSerializer.Deserialize<HeatmapDataDto>(cachedData);
            if (result != null) return Result<HeatmapDataDto>.Success(result);
        }

        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);

        var data = await _uow.ReviewLogs.GetHeatmapDataAsync(userId, from, to, ct);
        var entries = data.Select(d => new HeatmapEntryDto(d.Date, d.Count)).ToList();
        var heatmapDto = new HeatmapDataDto(entries, year);

        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4) };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(heatmapDto), options, ct);

        return Result<HeatmapDataDto>.Success(heatmapDto);
    }
}

// ─── Streak ─────────────────────────────────────────────────────
public record GetStreakQuery : IRequest<Result<StreakDto>>;

public class GetStreakHandler : IRequestHandler<GetStreakQuery, Result<StreakDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public GetStreakHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result<StreakDto>> Handle(GetStreakQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        
        var version = await _cache.GetStringAsync($"vocab-v:{userId}", ct) ?? "0";
        var cacheKey = $"analytics-streak:{userId}:v{version}";

        var cachedData = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var result = JsonSerializer.Deserialize<StreakDto>(cachedData);
            if (result != null) return Result<StreakDto>.Success(result);
        }

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

        var streakDto = new StreakDto(
            currentStreak,
            longestStreak,
            lastActiveDate == default ? null : lastActiveDate);

        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4) };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(streakDto), options, ct);

        return Result<StreakDto>.Success(streakDto);
    }
}
