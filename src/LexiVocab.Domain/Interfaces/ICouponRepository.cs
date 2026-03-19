using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

public interface ICouponRepository : IRepository<Coupon>
{
    Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<(IReadOnlyList<Coupon> Items, int TotalCount)> GetPagedAsync(
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}
