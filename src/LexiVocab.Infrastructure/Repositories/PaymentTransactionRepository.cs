using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Repositories;

public class PaymentTransactionRepository : GenericRepository<PaymentTransaction>, IPaymentTransactionRepository
{
    public PaymentTransactionRepository(AppDbContext context) : base(context) { }

    public async Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> GetPaginatedByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default)
        => await _dbSet.CountAsync(t => t.UserId == userId, ct);
}
