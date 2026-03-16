using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Infrastructure.Services;

public class FeatureGatingService : IFeatureGatingService
{
    private readonly IUnitOfWork _uow;
    private readonly Microsoft.Extensions.Caching.Distributed.IDistributedCache _cache;

    public FeatureGatingService(IUnitOfWork uow, Microsoft.Extensions.Caching.Distributed.IDistributedCache cache)
    {
        _uow = uow;
        _cache = cache;
    }

    public async Task<UserPermissionsDto> GetPermissionsAsync(Guid userId, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);

        var currentCount = await _uow.Vocabularies
            .CountByUserIdAsync(userId, ct);

        // Fetch active subscription with its plan and features
        var activeSub = await _uow.Subscriptions.GetActiveWithFeaturesAsync(userId, ct);

        // If no active sub (or expired), fall back to Free tier
        if (activeSub == null || (activeSub.EndDate.HasValue && activeSub.EndDate.Value < DateTime.UtcNow))
        {
            var freePlan = await GetPlanByCodeAsync("Free", ct);
            return CreatePermissionsDto(freePlan, currentCount, null);
        }

        return CreatePermissionsDto(activeSub.PlanDefinition, currentCount, activeSub.EndDate);
    }

    public async Task<bool> HasFeatureAsync(Guid userId, string featureCode, CancellationToken ct)
    {
        var permissions = await GetPermissionsAsync(userId, ct);
        return permissions.FeatureFlags.TryGetValue(featureCode, out var value) && 
               (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1"));
    }

    public async Task<bool> ConsumeQuotaAsync(Guid userId, string featureCode, string quotaLimitCode, CancellationToken ct)
    {
        // 1. Check if user has basic access to the feature
        if (!await HasFeatureAsync(userId, featureCode, ct)) return false;

        // 2. Get the limit from plan
        var permissions = await GetPermissionsAsync(userId, ct);
        if (!permissions.FeatureFlags.TryGetValue(quotaLimitCode, out var limitStr) || 
            !int.TryParse(limitStr, out var limit))
        {
            return false; // No limit defined = no access for safety
        }

        // 3. Check current usage in Cache
        var dateKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var cacheKey = $"quota:{userId}:{quotaLimitCode}:{dateKey}";
        
        var currentStr = await _cache.GetStringAsync(cacheKey, ct);
        int current = string.IsNullOrEmpty(currentStr) ? 0 : int.Parse(currentStr);

        if (current >= limit) return false;

        // 4. Increment and update Cache (simple race condition possible here, but acceptable for this MVP)
        // In a high-load scenario, we would use a Redis script (LUA) for atomic INCR and EXPIRE.
        await _cache.SetStringAsync(cacheKey, (current + 1).ToString(), new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(2) // Expire after 2 days to keep cache clean
        }, ct);

        return true;
    }

    private async Task<Domain.Entities.PlanDefinition?> GetPlanByCodeAsync(string name, CancellationToken ct)
    {
        return await _uow.PlanDefinitions.GetByNameWithFeaturesAsync(name, ct);
    }

    private static UserPermissionsDto CreatePermissionsDto(Domain.Entities.PlanDefinition? plan, int currentCount, DateTime? expiration)
    {
        if (plan == null) return new UserPermissionsDto("None", currentCount, expiration, new Dictionary<string, string>());

        var flags = plan.PlanFeatures.ToDictionary(pf => pf.Feature.Code, pf => pf.Value);

        return new UserPermissionsDto(
            Plan: plan.Name,
            CurrentCount: currentCount,
            PlanExpiresAt: expiration,
            FeatureFlags: flags);
    }

}
