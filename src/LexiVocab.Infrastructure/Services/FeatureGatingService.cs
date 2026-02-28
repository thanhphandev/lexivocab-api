using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Enums;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LexiVocab.Infrastructure.Services;

public class FeatureGatingService : IFeatureGatingService
{
    private readonly AppDbContext _db;
    private readonly int _freeMaxVocabularies;

    public FeatureGatingService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _freeMaxVocabularies = config.GetValue<int>("FreePlan:MaxVocabularies", 50);
    }

    public async Task<bool> IsPremiumAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null) return false;

        // Manual override for Admin role or Premium role
        if (user.Role == UserRole.Admin || user.Role == UserRole.Premium)
        {
            return true;
        }

        // Alternatively check plan expiration date
        if (user.PlanExpirationDate.HasValue && user.PlanExpirationDate.Value > DateTime.UtcNow)
        {
            return true;
        }

        return false;
    }

    public async Task<UserPermissionsDto> GetPermissionsAsync(Guid userId, CancellationToken ct)
    {
        var isPremium = await IsPremiumAsync(userId, ct);
        var currentCount = await _db.UserVocabularies
            .CountAsync(v => v.UserId == userId, ct);

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        return new UserPermissionsDto(
            Plan: isPremium ? "Premium" : "Free",
            MaxVocabularies: isPremium ? -1 : _freeMaxVocabularies,
            CurrentCount: currentCount,
            CanExportData: isPremium,
            CanUseAi: isPremium,
            CanBatchImport: isPremium,
            PlanExpiresAt: user?.PlanExpirationDate
        );
    }

    public async Task<bool> CanCreateVocabularyAsync(Guid userId, CancellationToken ct)
    {
        var isPremium = await IsPremiumAsync(userId, ct);
        if (isPremium) return true;

        var currentCount = await _db.UserVocabularies
            .CountAsync(v => v.UserId == userId, ct);

        return currentCount < _freeMaxVocabularies;
    }
}
