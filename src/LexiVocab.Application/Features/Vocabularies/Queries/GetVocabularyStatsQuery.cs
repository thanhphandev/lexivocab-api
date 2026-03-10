using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace LexiVocab.Application.Features.Vocabularies.Queries;

public record GetVocabularyStatsQuery : IRequest<Result<VocabularyStatsDto>>;

public class GetVocabularyStatsHandler : IRequestHandler<GetVocabularyStatsQuery, Result<VocabularyStatsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public GetVocabularyStatsHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result<VocabularyStatsDto>> Handle(GetVocabularyStatsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        
        var version = await _cache.GetStringAsync($"vocab-v:{userId}", ct) ?? "0";
        var cacheKey = $"vocab-stats:{userId}:v{version}";
        
        var cachedData = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var result = JsonSerializer.Deserialize<VocabularyStatsDto>(cachedData);
            if (result != null) return Result<VocabularyStatsDto>.Success(result);
        }

        var (total, active, archived, dueToday) = await _uow.Vocabularies.GetStatsAsync(userId, ct);
        var stats = new VocabularyStatsDto(total, active, archived, dueToday);
        
        var options = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(2) };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(stats), options, ct);

        return Result<VocabularyStatsDto>.Success(stats);
    }
}
