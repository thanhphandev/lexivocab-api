using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LexiVocab.Infrastructure.Services;

public class FeatureGatingService : IFeatureGatingService
{
    private readonly IUnitOfWork _uow;
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly IDateTimeProvider _dateTime;

    private const string RedisKeyPrefix = "LexiVocab:";
    private const int QuotaCacheTtlDays = 2;

    public FeatureGatingService(IUnitOfWork uow, IDistributedCache cache, IDateTimeProvider dateTime)
        : this(uow, cache, dateTime, null)
    {
    }

    public FeatureGatingService(IUnitOfWork uow, IDistributedCache cache, IDateTimeProvider dateTime, IConnectionMultiplexer? redis)
    {
        _uow = uow;
        _cache = cache;
        _dateTime = dateTime;
        _redis = redis;
    }

    public async Task<UserPermissionsDto> GetPermissionsAsync(Guid userId, CancellationToken ct)
    {
        var currentCount = await _uow.Vocabularies
            .CountByUserIdAsync(userId, ct);

        // Fetch active subscription with its plan and features
        var activeSub = await _uow.Subscriptions.GetActiveWithFeaturesAsync(userId, ct);

        // Fetch AI Quota usages
        var dateKey = _dateTime.UtcNow.ToString("yyyy-MM-dd");
        var aiKey = $"quota:{userId}:AI_DAILY_LIMIT:{dateKey}";
        var transKey = $"quota:{userId}:LLM_TRANSLATION_LIMIT:{dateKey}";
        var quizKey = $"quota:{userId}:MAX_QUIZ_PER_DAY:{dateKey}";
        
        int aiUsage = 0, transUsage = 0, quizUsage = 0;
        
        if (_redis is not null)
        {
            var db = _redis.GetDatabase();
            var vals = await db.StringGetAsync(new RedisKey[] { RedisKeyPrefix + aiKey, RedisKeyPrefix + transKey, RedisKeyPrefix + quizKey });
            if (vals[0].HasValue) aiUsage = (int)vals[0];
            if (vals[1].HasValue) transUsage = (int)vals[1];
            if (vals[2].HasValue) quizUsage = (int)vals[2];
        }
        else
        {
            var aiStr = await _cache.GetStringAsync(aiKey, ct);
            if (!string.IsNullOrEmpty(aiStr)) aiUsage = int.Parse(aiStr);
            var transStr = await _cache.GetStringAsync(transKey, ct);
            if (!string.IsNullOrEmpty(transStr)) transUsage = int.Parse(transStr);
            var quizStr = await _cache.GetStringAsync(quizKey, ct);
            if (!string.IsNullOrEmpty(quizStr)) quizUsage = int.Parse(quizStr);
        }

        var usages = new Dictionary<string, int>
        {
            { "AI_DAILY_LIMIT", aiUsage },
            { "LLM_TRANSLATION_LIMIT", transUsage },
            { "MAX_QUIZ_PER_DAY", quizUsage }
        };

        // If no active sub (or expired), fall back to Free tier
        if (activeSub == null || (activeSub.EndDate.HasValue && activeSub.EndDate.Value < _dateTime.UtcNow))
        {
            var freePlan = await GetPlanByCodeAsync("Free", ct);
            return CreatePermissionsDto(freePlan, currentCount, null, usages);
        }

        return CreatePermissionsDto(activeSub.PlanDefinition, currentCount, activeSub.EndDate, usages);
    }

    public async Task<bool> HasFeatureAsync(Guid userId, string featureCode, CancellationToken ct)
    {
        var permissions = await GetPermissionsAsync(userId, ct);
        return permissions.FeatureFlags.TryGetValue(featureCode, out var value) && 
               (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1"));
    }

    public async Task<bool> ConsumeQuotaAsync(Guid userId, string featureCode, string quotaLimitCode, CancellationToken ct)
    {
        // Single call to get full permissions (avoids N+1 double-call via HasFeatureAsync)
        var permissions = await GetPermissionsAsync(userId, ct);

        // 1. Check if user has basic access to the feature
        if (!permissions.FeatureFlags.TryGetValue(featureCode, out var value) ||
            !(value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1")))
            return false;

        // 2. Get the limit from plan
        var limit = permissions.GetLimit(quotaLimitCode, 0);

        if (limit == -1) return true; // Unlimited
        if (limit == 0) return false; // Strictly blocked

        // 3. Consume quota atomically in Redis if available
        var dateKey = _dateTime.UtcNow.ToString("yyyy-MM-dd");
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

        // 4. ⚠️ Dev-only fallback using distributed cache APIs.
        //    WARNING: This path is NOT atomic (read-then-write TOCTOU race condition).
        //    In production, Redis is REQUIRED for correct quota enforcement under concurrency.
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

    private static UserPermissionsDto CreatePermissionsDto(Domain.Entities.PlanDefinition? plan, int currentCount, DateTime? expiration, Dictionary<string, int> usages)
    {
        if (plan == null) return new UserPermissionsDto("None", currentCount, expiration, new Dictionary<string, string>(), usages ?? new(), 0);

        var flags = plan.PlanFeatures.ToDictionary(pf => pf.Feature.Code, pf => pf.Value);

        return new UserPermissionsDto(
            Plan: plan.Name,
            CurrentCount: currentCount,
            PlanExpiresAt: expiration,
            FeatureFlags: flags,
            QuotaUsages: usages ?? new(),
            DisplayOrder: plan.DisplayOrder);
    }

}
