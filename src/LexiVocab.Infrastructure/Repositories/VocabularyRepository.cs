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
        Guid userId, int page, int pageSize, bool? isArchived, string? searchTerm, CancellationToken ct)
    {
        var query = _dbSet
            .Include(v => v.MasterVocabulary)
            .Where(v => v.UserId == userId);

        if (isArchived.HasValue)
            query = query.Where(v => v.IsArchived == isArchived.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(v => EF.Functions.ILike(v.WordText, $"%{searchTerm}%")
                || (v.CustomMeaning != null && EF.Functions.ILike(v.CustomMeaning, $"%{searchTerm}%")));

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
        Guid userId, int limit, CancellationToken ct)
    {
        // THE critical query — hits the composite index (UserId, NextReviewDate, IsArchived)
        return await _dbSet
            .Include(v => v.MasterVocabulary)
            .Where(v => v.UserId == userId
                && v.NextReviewDate <= DateTime.UtcNow
                && !v.IsArchived)
            .OrderBy(v => v.NextReviewDate) // Oldest due first
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<bool> WordExistsForUserAsync(Guid userId, string wordText, CancellationToken ct)
        => await _dbSet.AnyAsync(
            v => v.UserId == userId && EF.Functions.ILike(v.WordText, wordText), ct);

    public async Task<(int Total, int Active, int Archived, int DueToday)> GetStatsAsync(
        Guid userId, CancellationToken ct)
    {
        var stats = await _dbSet
            .Where(v => v.UserId == userId)
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
}
