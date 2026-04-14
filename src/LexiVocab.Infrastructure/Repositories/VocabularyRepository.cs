using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Repositories;

public class VocabularyRepository : GenericRepository<UserVocabulary>, IVocabularyRepository
{
    public VocabularyRepository(AppDbContext context) : base(context) { }

    public override async Task<UserVocabulary?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbSet
            .Include(v => v.MasterVocabulary)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<(IReadOnlyList<UserVocabulary> Items, int TotalCount)> GetByUserIdAsync(
        Guid userId, int page, int pageSize, bool? isArchived, string? searchTerm, Guid? tagId, CancellationToken ct)
    {
        var query = _dbSet
            .Include(v => v.MasterVocabulary)
            .Where(v => v.UserId == userId);

        if (isArchived.HasValue)
            query = query.Where(v => v.IsArchived == isArchived.Value);

        if (tagId.HasValue)
            query = query.Where(v => v.TagId == tagId.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            // Fix Table Scan: Use ILike for DB-native case-insensitive search
            query = query.Where(v => EF.Functions.ILike(v.WordText, $"%{searchTerm}%")
                || (v.CustomMeaning != null && EF.Functions.ILike(v.CustomMeaning, $"%{searchTerm}%")));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<UserVocabulary> Items, int TotalCount)> GetByTagIdAsync(
        Guid userId, Guid tagId, int page, int pageSize, CancellationToken ct)
    {
        var query = _dbSet
            .Include(v => v.MasterVocabulary)
            .Where(v => v.UserId == userId && v.TagId == tagId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<UserVocabulary>> GetDueForReviewAsync(
        Guid userId, int reviewLimit, int newCardLimit, CancellationToken ct)
    {
        // New cards (RepetitionCount == 0)
        var newCards = await _dbSet
            .Include(v => v.MasterVocabulary)
            .Where(v => v.UserId == userId
                && v.NextReviewDate <= DateTime.UtcNow
                && !v.IsArchived
                && v.RepetitionCount == 0)
            .OrderBy(v => v.NextReviewDate)
            .Take(newCardLimit)
            .AsNoTracking()
            .ToListAsync(ct);

        // Review cards (RepetitionCount > 0)
        var reviewCards = await _dbSet
            .Include(v => v.MasterVocabulary)
            .Where(v => v.UserId == userId
                && v.NextReviewDate <= DateTime.UtcNow
                && !v.IsArchived
                && v.RepetitionCount > 0)
            .OrderBy(v => v.NextReviewDate)
            .Take(reviewLimit)
            .AsNoTracking()
            .ToListAsync(ct);

        var combined = new List<UserVocabulary>(newCards.Count + reviewCards.Count);
        combined.AddRange(newCards);
        combined.AddRange(reviewCards);
        return combined;
    }

    public async Task<bool> WordExistsForUserAsync(Guid userId, string wordText, CancellationToken ct)
    {
        var lowerWord = wordText.ToLower();
        return await _dbSet.AnyAsync(
            v => v.UserId == userId && v.WordText.ToLower() == lowerWord, ct);
    }

    public async Task<(int Total, int Active, int Archived, int DueToday)> GetStatsAsync(
        Guid userId, CancellationToken ct)
    {
        var stats = await _dbSet
            .Where(v => v.UserId == userId)
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(v => !v.IsArchived),
                Archived = g.Count(v => v.IsArchived),
                DueToday = g.Count(v => !v.IsArchived && v.NextReviewDate <= DateTime.UtcNow)
            })
            .FirstOrDefaultAsync(ct);

        return stats is null ? (0, 0, 0, 0) : (stats.Total, stats.Active, stats.Archived, stats.DueToday);
    }

    public async Task<(double RetentionRate, double LearningProgress, int WordsLearnedThisWeek, List<string> MostDifficultWords, Dictionary<string, int> CefrSpread)> GetInDepthStatsAsync(
        Guid userId, CancellationToken ct = default)
    {
        // 1. Basic Progress & This Week
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var baseStats = await _dbSet
            .Where(v => v.UserId == userId)
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Mastered = g.Count(v => v.IsArchived),
                LearnedThisWeek = g.Count(v => v.CreatedAt >= weekAgo)
            })
            .FirstOrDefaultAsync(ct);

        int total = baseStats?.Total ?? 0;
        int learnedThisWeek = baseStats?.LearnedThisWeek ?? 0;
        double learningProgress = total > 0 ? (baseStats!.Mastered * 100.0 / total) : 0.0;

        // 2. Retention Rate (Correct Reviews / Total Reviews) -> EF Core optimized count
        var reviewStats = await _context.Set<ReviewLog>()
            .Where(r => r.UserId == userId)
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalReviews = g.Count(),
                CorrectReviews = g.Count(r => (int)r.QualityScore >= 3)
            })
            .FirstOrDefaultAsync(ct);

        double retentionRate = reviewStats?.TotalReviews > 0 
            ? (reviewStats.CorrectReviews * 100.0 / reviewStats.TotalReviews) 
            : 0.0;

        // 3. Most Difficult Words (Lowest Easiness Factor)
        var difficultWords = await _dbSet
            .Where(v => v.UserId == userId && !v.IsArchived && v.RepetitionCount > 0)
            .OrderBy(v => v.EasinessFactor)
            .Select(v => v.WordText)
            .Take(6)
            .AsNoTracking()
            .ToListAsync(ct);

        // 4. CEFR Spread
        var cefrList = await _dbSet
            .Where(v => v.UserId == userId && v.MasterVocabularyId != null && v.MasterVocabulary!.CefrLevel != null)
            .GroupBy(v => v.MasterVocabulary!.CefrLevel)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .AsNoTracking()
            .ToListAsync(ct);

        var cefrDict = cefrList.ToDictionary(k => k.Level!, v => v.Count);

        // Fill out empty standard CEFR levels so UI doesn't break
        var standardLevels = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };
        foreach (var level in standardLevels)
        {
            if (!cefrDict.ContainsKey(level)) cefrDict[level] = 0;
        }

        return (Math.Round(retentionRate, 1), Math.Round(learningProgress, 1), learnedThisWeek, difficultWords, cefrDict);
    }

    public async Task<int> CountByUserIdAsync(Guid userId, CancellationToken ct)
        => await _dbSet.CountAsync(v => v.UserId == userId, ct);

    public async Task<HashSet<string>> GetExistingWordsAsync(Guid userId, IEnumerable<string> words, CancellationToken ct = default)
    {
        var lowerWords = words.Select(w => w.ToLower()).ToList();
        var existingWords = await _dbSet
            .Where(v => v.UserId == userId && lowerWords.Contains(v.WordText.ToLower()))
            .Select(v => v.WordText.ToLower())
            .ToListAsync(ct);
        return existingWords.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<List<string>> GetUnlinkedWordsAsync(int limit = 100, CancellationToken ct = default)
        => await _dbSet
            .Where(v => v.MasterVocabularyId == null)
            .Select(v => v.WordText.ToLower())
            .Distinct()
            .Take(limit)
            .ToListAsync(ct);

    public async Task LinkToMasterAsync(string word, Guid masterId, CancellationToken ct = default)
    {
        var lowerWord = word.ToLower();
        await _dbSet
            .Where(v => v.MasterVocabularyId == null && v.WordText.ToLower() == lowerWord)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.MasterVocabularyId, masterId), ct);
    }
}
