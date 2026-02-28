using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAuditLogRepository"/>.
/// Uses <c>AddAsync</c>/<c>SaveChangesAsync</c> for immediate persistence
/// to ensure audit records are never lost in transaction rollbacks.
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog log, CancellationToken ct = default)
    {
        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<AuditLog> logs, CancellationToken ct = default)
    {
        _context.AuditLogs.AddRange(logs);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPagedAsync(
        Guid? userId = null,
        AuditAction? action = null,
        string? entityType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        if (action.HasValue)
            query = query.Where(a => a.Action == action.Value);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (fromDate.HasValue)
            query = query.Where(a => a.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.Timestamp <= toDate.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<int> CountRecentFailedLoginsAsync(
        string ipAddress, TimeSpan window, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - window;

        return await _context.AuditLogs
            .AsNoTracking()
            .CountAsync(a =>
                a.IpAddress == ipAddress &&
                a.Action == AuditAction.LoginFailed &&
                a.Timestamp >= cutoff &&
                !a.IsSuccess, ct);
    }
}
