using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Repositories;

public class SubscriptionRepository : GenericRepository<Subscription>, ISubscriptionRepository
{
    public SubscriptionRepository(AppDbContext context) : base(context) { }

    public async Task<Subscription?> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Include(s => s.PlanDefinition)
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(ct);

    public async Task<Subscription?> GetActiveWithFeaturesAsync(Guid userId, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Include(s => s.PlanDefinition)
                .ThenInclude(p => p.PlanFeatures)
                    .ThenInclude(pf => pf.Feature)
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Subscription>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Include(s => s.PlanDefinition)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);

    public async Task<int> CountActiveAsync(CancellationToken ct = default)
        => await _dbSet.CountAsync(s => s.Status == SubscriptionStatus.Active, ct);

    public async Task<IReadOnlyList<Subscription>> GetExpiredWithUserAsync(DateTime now, CancellationToken ct = default)
        => await _dbSet
            .Include(s => s.User)
            .Where(s => s.Status == SubscriptionStatus.Active && 
                        s.EndDate.HasValue && 
                        s.EndDate.Value < now)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Subscription>> GetExpiringSoonWithUserAsync(DateTime now, DateTime threshold, CancellationToken ct = default)
        => await _dbSet
            .Include(s => s.User)
            .Where(s => s.Status == SubscriptionStatus.Active &&
                        s.EndDate.HasValue &&
                        s.EndDate.Value > now &&
                        s.EndDate.Value <= threshold)
            .ToListAsync(ct);
}
