using System.Text.Json;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Common;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Common.Behaviors;

/// <summary>
/// Intercepts MediatR queries implementing <see cref="ICacheableQuery{TResponse}"/> 
/// and attempts to fetch the result from DistributedCache before executing the handler.
/// </summary>
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICacheableQuery<TResponse>
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(IDistributedCache cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var cacheKey = request.CacheKey;

        try
        {
            var cachedResponseBytes = await _cache.GetAsync(cacheKey, cancellationToken);
            if (cachedResponseBytes != null)
            {
                _logger.LogInformation("Cache hit for {QueryName} with key {CacheKey}", typeof(TRequest).Name, cacheKey);
                
                // Assuming responses are JSON serializable Result<T> objects often, or raw responses
                var cachedResponse = JsonSerializer.Deserialize<TResponse>(cachedResponseBytes);
                if (cachedResponse != null)
                {
                    return cachedResponse;
                }
            }
        }
        catch (Exception ex)
        {
            // If cache fails (Redis down), log and proceed to DB. Do not fail the request.
            _logger.LogWarning(ex, "Failed to read from cache for {QueryName} with key {CacheKey}", typeof(TRequest).Name, cacheKey);
        }

        _logger.LogInformation("Cache miss for {QueryName} with key {CacheKey}. Executing handler...", typeof(TRequest).Name, cacheKey);
        
        // Execute the actual handler (e.g. hitting the database)
        var response = await next();

        try
        {
            // We should only cache successful Result<T> objects to avoid caching errors (e.g., 404s, 500s)
            if (response is IResult result && !result.IsSuccess)
            {
                return response;
            }

            var duration = request.CacheDuration ?? TimeSpan.FromMinutes(5); // Default 5 mins
            var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = duration };
            
            var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response);
            await _cache.SetAsync(cacheKey, responseBytes, cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write to cache for {QueryName} with key {CacheKey}", typeof(TRequest).Name, cacheKey);
        }

        return response;
    }
}
