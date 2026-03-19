using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
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

    public async Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> GetPagedForAdminAsync(
        DateTime? fromDate = null, DateTime? toDate = null, string? status = null, string? provider = null, string? search = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var query = _dbSet
            .Include(t => t.User)
            .Include(t => t.Subscription)
            .Include(t => t.Coupon)
            .AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(t => t.CreatedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(t => t.CreatedAt <= toDate.Value);
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<PaymentStatus>(status, true, out var parsedStatus))
            query = query.Where(t => t.Status == parsedStatus);
        if (!string.IsNullOrEmpty(provider) && Enum.TryParse<PaymentProvider>(provider, true, out var parsedProvider))
            query = query.Where(t => t.Provider == parsedProvider);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(t => 
                t.ExternalOrderId.Contains(s) || 
                t.User.Email.Contains(s) || 
                (t.Coupon != null && t.Coupon.Code.Contains(s)));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

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

    public async Task<IReadOnlyList<PaymentTransaction>> GetExpiredPendingByUserAsync(
        Guid userId, DateTime now, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(t => t.Subscription)
            .Where(t =>
                t.UserId == userId &&
                t.Status == PaymentStatus.Pending &&
                t.ExpiresAt.HasValue &&
                t.ExpiresAt.Value <= now)
            .ToListAsync(ct);
    }
}
