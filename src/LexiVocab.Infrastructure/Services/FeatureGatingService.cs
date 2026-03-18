using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace LexiVocab.Infrastructure.Services;

public class FeatureGatingService : IFeatureGatingService
{
    private readonly IUnitOfWork _uow;
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _redis;

    private const string RedisKeyPrefix = "LexiVocab:";
    private const int QuotaCacheTtlDays = 2;

    public FeatureGatingService(IUnitOfWork uow, IDistributedCache cache)
        : this(uow, cache, null)
    {
    }

    public FeatureGatingService(IUnitOfWork uow, IDistributedCache cache, IConnectionMultiplexer? redis)
    {
        _uow = uow;
        _cache = cache;
        _redis = redis;
    }

    public async Task<UserPermissionsDto> GetPermissionsAsync(Guid userId, CancellationToken ct)
    {
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

        // 3. Consume quota atomically in Redis if available
        var dateKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var cacheKey = $"quota:{userId}:{quotaLimitCode}:{dateKey}";

        if (_redis is not null)
        {
            var db = _redis.GetDatabase();
            var redisKey = RedisKeyPrefix + cacheKey;
            var ttlSeconds = QuotaCacheTtlDays * 24 * 60 * 60;

            // Atomic check-and-increment to avoid quota overshoot under concurrency.
            const string script = @"
local current = tonumber(redis.call('GET', KEYS[1]) or '0')
local limit = tonumber(ARGV[1])
local ttl = tonumber(ARGV[2])

if current >= limit then
    return -1
end

current = redis.call('INCR', KEYS[1])

if current == 1 then
    redis.call('EXPIRE', KEYS[1], ttl)
end

if current > limit then
    redis.call('DECR', KEYS[1])
    return -1
end

return current
";

            var result = (long)await db.ScriptEvaluateAsync(
                script,
                [redisKey],
                [limit, ttlSeconds]);

            return result != -1;
        }

        // 4. Fallback for non-Redis environments (development) using distributed cache APIs.
        var currentStr = await _cache.GetStringAsync(cacheKey, ct);
        var current = string.IsNullOrEmpty(currentStr) ? 0 : int.Parse(currentStr);
        if (current >= limit) return false;

        await _cache.SetStringAsync(cacheKey, (current + 1).ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(QuotaCacheTtlDays)
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
