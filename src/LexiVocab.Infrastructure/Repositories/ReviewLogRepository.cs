using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Repositories;

public class ReviewLogRepository : GenericRepository<ReviewLog>, IReviewLogRepository
{
    public ReviewLogRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<(DateOnly Date, int Count)>> GetHeatmapDataAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fromDate = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toDate = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        // Use raw SQL date truncation for compatibility with EF Core 10 + Npgsql
        var data = await _dbSet
            .Where(r => r.UserId == userId
                && r.ReviewedAt >= fromDate
                && r.ReviewedAt <= toDate)
            .GroupBy(r => new { r.ReviewedAt.Year, r.ReviewedAt.Month, r.ReviewedAt.Day })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Day = g.Key.Day,
                Count = g.Count()
            })
            .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
            .AsNoTracking()
            .ToListAsync(ct);

        return data.Select(d => (new DateOnly(d.Year, d.Month, d.Day), d.Count)).ToList();
    }

    public async Task<int> GetCurrentStreakAsync(Guid userId, CancellationToken ct)
    {
        // Get distinct dates with reviews, ordered descending  
        // Use Year/Month/Day components to avoid .Date translation issues
        var reviewDates = await _dbSet
            .Where(r => r.UserId == userId)
            .Select(r => new { r.ReviewedAt.Year, r.ReviewedAt.Month, r.ReviewedAt.Day })
            .Distinct()
            .OrderByDescending(d => d.Year).ThenByDescending(d => d.Month).ThenByDescending(d => d.Day)
            .Take(365)
            .AsNoTracking()
            .ToListAsync(ct);

        if (reviewDates.Count == 0)
            return 0;

        var dates = reviewDates.Select(d => new DateOnly(d.Year, d.Month, d.Day)).ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var streak = 0;
        var expectedDate = today;

        // If the user hasn't reviewed today, start from yesterday
        if (dates[0] != today)
        {
            if (dates[0] != today.AddDays(-1))
                return 0; // Streak broken

            expectedDate = today.AddDays(-1);
        }

        foreach (var date in dates)
        {
            if (date == expectedDate)
            {
                streak++;
                expectedDate = expectedDate.AddDays(-1);
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    public async Task<IReadOnlyList<ReviewLog>> GetByVocabularyIdAsync(
        Guid userVocabularyId, CancellationToken ct)
    {
        return await _dbSet
            .Where(r => r.UserVocabularyId == userVocabularyId)
            .OrderByDescending(r => r.ReviewedAt)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<(int TotalReviews, double AvgQuality, int TotalTimeMs)> GetPeriodStatsAsync(
        Guid userId, DateTime from, DateTime to, CancellationToken ct)
    {
        var stats = await _dbSet
            .Where(r => r.UserId == userId && r.ReviewedAt >= from && r.ReviewedAt < to)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                AvgQuality = g.Average(r => (double)(int)r.QualityScore),
                TotalTime = g.Sum(r => r.TimeSpentMs ?? 0)
            })
            .FirstOrDefaultAsync(ct);

        return stats is null ? (0, 0, 0) : (stats.Total, Math.Round(stats.AvgQuality, 2), stats.TotalTime);
    }
}
