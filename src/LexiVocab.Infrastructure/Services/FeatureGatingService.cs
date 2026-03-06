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

        return IsUserPremium(user);
    }

    public async Task<UserPermissionsDto> GetPermissionsAsync(Guid userId, CancellationToken ct)
    {
        // Single query: fetch user and count in parallel to avoid redundant DB calls
        var userTask = _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        var countTask = _db.UserVocabularies
            .CountAsync(v => v.UserId == userId, ct);

        await Task.WhenAll(userTask, countTask);

        var user = userTask.Result;
        var currentCount = countTask.Result;

        if (user == null)
        {
            return new UserPermissionsDto(
                Plan: "Free",
                MaxVocabularies: _freeMaxVocabularies,
                CurrentCount: 0,
                CanExportData: false,
                CanUseAi: false,
                CanBatchImport: false,
                PlanExpiresAt: null);
        }

        var isPremium = IsUserPremium(user);

        return new UserPermissionsDto(
            Plan: isPremium ? "Premium" : "Free",
            MaxVocabularies: isPremium ? -1 : _freeMaxVocabularies,
            CurrentCount: currentCount,
            CanExportData: isPremium,
            CanUseAi: isPremium,
            CanBatchImport: isPremium,
            PlanExpiresAt: user.PlanExpirationDate
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

    /// <summary>
    /// Shared logic to determine premium status from a User entity.
    /// Avoids duplicating the check across multiple methods.
    /// </summary>
    private static bool IsUserPremium(Domain.Entities.User user)
    {
        // Manual override for Admin or Premium role
        if (user.Role is UserRole.Admin or UserRole.Premium)
            return true;

        // Check plan expiration date
        if (user.PlanExpirationDate.HasValue && user.PlanExpirationDate.Value > DateTime.UtcNow)
            return true;

        return false;
    }
}
