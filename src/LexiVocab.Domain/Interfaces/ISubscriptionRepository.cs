using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Domain.Interfaces;

/// <summary>
/// Subscription repository for billing queries.
/// </summary>
public interface ISubscriptionRepository : IRepository<Subscription>
{
    Task<Subscription?> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Subscription?> GetActiveWithFeaturesAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Subscription>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<int> CountActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Subscription>> GetExpiredWithUserAsync(DateTime now, CancellationToken ct = default);
    Task<IReadOnlyList<Subscription>> GetExpiringSoonWithUserAsync(DateTime now, DateTime threshold, CancellationToken ct = default);
}
