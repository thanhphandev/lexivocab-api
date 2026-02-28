using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

/// <summary>
/// Payment transaction repository for billing history queries.
/// </summary>
public interface IPaymentTransactionRepository : IRepository<PaymentTransaction>
{
    Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> GetPaginatedByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);

    Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default);
}
