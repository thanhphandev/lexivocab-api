using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Repositories;

public class MasterVocabularyRepository : GenericRepository<MasterVocabulary>, IMasterVocabularyRepository
{
    public MasterVocabularyRepository(AppDbContext context) : base(context) { }

    public async Task<MasterVocabulary?> GetByWordAsync(string word, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(
            m => m.Word == word, ct);

    public async Task<IReadOnlyList<MasterVocabulary>> SearchAsync(
        string query, int limit = 10, CancellationToken ct = default)
        => await _dbSet
            .Where(m => EF.Functions.ILike(m.Word, $"{query}%")) // Prefix match for autocomplete
            .OrderBy(m => m.PopularityRank ?? int.MaxValue) // Most popular first
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<MasterVocabulary> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? searchQuery = null, CancellationToken ct = default)
    {
        var query = _dbSet.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            query = query.Where(m => EF.Functions.ILike(m.Word, $"%{searchQuery}%"));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(m => m.Word)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Dictionary<string, MasterVocabulary>> GetByWordsAsync(IEnumerable<string> words, CancellationToken ct = default)
    {
        var wordList = words.ToList();
        var results = await _dbSet
            .Where(m => wordList.Contains(m.Word))
            .ToListAsync(ct);
        return results.ToDictionary(m => m.Word, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<MasterVocabulary>> GetPendingEnrichmentAsync(int limit = 50, CancellationToken ct = default)
        => await _dbSet
            .Where(m => string.IsNullOrEmpty(m.PhoneticUk) || string.IsNullOrEmpty(m.AudioUrl))
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
}
