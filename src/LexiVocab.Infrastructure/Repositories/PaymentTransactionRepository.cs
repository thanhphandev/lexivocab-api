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
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

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

    public async Task<PaymentTransaction?> GetByExternalOrderIdAsync(string externalOrderId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(externalOrderId)) return null;
        
        return await _dbSet
            .Include(t => t.Subscription)
            .ThenInclude(s => s!.User)
            .FirstOrDefaultAsync(t => t.ExternalOrderId == externalOrderId, ct);
    }

    public async Task<PaymentTransaction?> GetByExternalOrderIdWithDetailsAsync(string orderId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(orderId)) return null;
        
        return await _dbSet
            .Include(t => t.Subscription)
                .ThenInclude(s => s!.User)
            .FirstOrDefaultAsync(t => t.ExternalOrderId == orderId, ct);
    }

    public async Task<bool> ExistsByProviderResponseIdAsync(string responseId, CancellationToken ct = default)
        => !string.IsNullOrEmpty(responseId) && await _dbSet.AnyAsync(t => t.ProviderResponseId == responseId, ct);
}
