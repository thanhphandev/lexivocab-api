using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

/// <summary>
/// User-specific repository operations beyond generic CRUD.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByAuthProviderAsync(string provider, string providerId, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetPaginatedAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<int> CountPremiumUsersAsync(CancellationToken ct = default);
}
