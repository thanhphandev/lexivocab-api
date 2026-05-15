using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

using LexiVocab.Application.Common.Extensions;

namespace LexiVocab.Application.Features.Vocabularies.Queries;

public record GetVocabularyInDepthStatsQuery : IRequest<Result<VocabularyInDepthStatsDto>>;

public class GetVocabularyInDepthStatsHandler : IRequestHandler<GetVocabularyInDepthStatsQuery, Result<VocabularyInDepthStatsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public GetVocabularyInDepthStatsHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result<VocabularyInDepthStatsDto>> Handle(GetVocabularyInDepthStatsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();
        
        var version = await _cache.GetStringAsync($"vocab-v:{userId}", ct) ?? "0";
        var cacheKey = $"vocab-stats-indepth:{userId}:v{version}";
        
        var cachedData = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var result = JsonSerializer.Deserialize<VocabularyInDepthStatsDto>(cachedData);
            if (result != null) return Result<VocabularyInDepthStatsDto>.Success(result);
        }

        var (retentionRate, learningProgress, wordsLearnedThisWeek, mostDifficultWords, cefrSpread) = await _uow.Vocabularies.GetInDepthStatsAsync(userId, ct);
        var stats = new VocabularyInDepthStatsDto(retentionRate, learningProgress, wordsLearnedThisWeek, mostDifficultWords, cefrSpread);
        
        var options = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(15) };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(stats), options, ct);

        return Result<VocabularyInDepthStatsDto>.Success(stats);
    }
}
