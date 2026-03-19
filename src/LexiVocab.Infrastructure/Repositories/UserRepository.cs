using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public override async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbSet
            .Include(u => u.UserSetting)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<User?> GetByAuthProviderAsync(string provider, string providerId, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(
            u => u.AuthProvider == provider && u.AuthProviderId == providerId, ct);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => await _dbSet.AnyAsync(u => u.Email == email, ct);

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetPaginatedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(u => u.Email.ToLower().Contains(search) || u.FullName.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<int> CountPremiumUsersAsync(CancellationToken ct = default)
        => await _context.Subscriptions
            .Where(s => s.Status == LexiVocab.Domain.Enums.SubscriptionStatus.Active)
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(ct);

    public async Task<int> CountActiveSinceAsync(DateTime since, CancellationToken ct = default)
        => await _dbSet.CountAsync(u => u.LastLogin >= since, ct);

    public async Task<IReadOnlyList<(string Date, int Count)>> GetNewUsersCountByDateAsync(DateTime since, CancellationToken ct = default)
    {
        var data = await _dbSet
            .Where(u => u.CreatedAt >= since)
            .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month, u.CreatedAt.Day })
            .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Day = g.Key.Day, Count = g.Count() })
            .ToListAsync(ct);

        return data.Select(x => (new DateOnly(x.Year, x.Month, x.Day).ToString("yyyy-MM-dd"), x.Count)).OrderBy(x => x.Item1).ToList();
    }
}
