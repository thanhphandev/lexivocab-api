using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.Text;

namespace LexiVocab.Application.Features.Vocabularies.Queries;

// ─── Get Paginated Vocabulary List ──────────────────────────────
public record GetVocabularyListQuery(
    int Page = 1,
    int PageSize = 20,
    bool? IsArchived = null,
    string? SearchTerm = null,
    Guid? TagId = null
) : IRequest<Result<PagedResult<VocabularyDto>>>;

public class GetVocabularyListHandler : IRequestHandler<GetVocabularyListQuery, Result<PagedResult<VocabularyDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public GetVocabularyListHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result<PagedResult<VocabularyDto>>> Handle(GetVocabularyListQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        
        // Retrieve or initialize Cache Buster Version
        var version = await _cache.GetStringAsync($"vocab-v:{userId}", ct) ?? "0";
        var cacheKey = $"vocab-list:{userId}:v{version}:p{request.Page}:s{request.PageSize}:a{request.IsArchived}:q{request.SearchTerm}";
        
        var cachedData = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var result = JsonSerializer.Deserialize<PagedResult<VocabularyDto>>(cachedData);
            if (result != null) return Result<PagedResult<VocabularyDto>>.Success(result);
        }

        var (items, totalCount) = await _uow.Vocabularies.GetByUserIdAsync(
            userId, request.Page, request.PageSize, request.IsArchived, request.SearchTerm, request.TagId, ct);

        var dtos = items.Select(MapToDto).ToList();
        var pagedResult = new PagedResult<VocabularyDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
        
        var options = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(1) };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(pagedResult), options, ct);

        return Result<PagedResult<VocabularyDto>>.Success(pagedResult);
    }

    private static VocabularyDto MapToDto(UserVocabulary v) => new(
        v.Id, v.TagId, v.WordText, v.CustomMeaning, v.ContextSentence, v.SourceUrl,
        v.RepetitionCount, v.EasinessFactor, v.IntervalDays,
        v.NextReviewDate, v.LastReviewedAt, v.IsArchived, v.CreatedAt,
        v.MasterVocabulary?.PhoneticUk, v.MasterVocabulary?.PhoneticUs,
        v.MasterVocabulary?.AudioUrl, v.MasterVocabulary?.PartOfSpeech);
}

// ─── Get Single Vocabulary by Id ────────────────────────────────
public record GetVocabularyByIdQuery(Guid Id) : IRequest<Result<VocabularyDto>>;

public class GetVocabularyByIdHandler : IRequestHandler<GetVocabularyByIdQuery, Result<VocabularyDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public GetVocabularyByIdHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result<VocabularyDto>> Handle(GetVocabularyByIdQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        
        var version = await _cache.GetStringAsync($"vocab-v:{userId}", ct) ?? "0";
        var cacheKey = $"vocab-item:{userId}:{request.Id}:v{version}";

        var cachedData = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var result = JsonSerializer.Deserialize<VocabularyDto>(cachedData);
            if (result != null) return Result<VocabularyDto>.Success(result);
        }

        var entity = await _uow.Vocabularies.GetByIdAsync(request.Id, ct);
        if (entity is null || entity.UserId != userId)
            return Result<VocabularyDto>.NotFound("Vocabulary not found.");

        var dto = new VocabularyDto(
            entity.Id, entity.TagId, entity.WordText, entity.CustomMeaning, entity.ContextSentence, entity.SourceUrl,
            entity.RepetitionCount, entity.EasinessFactor, entity.IntervalDays,
            entity.NextReviewDate, entity.LastReviewedAt, entity.IsArchived, entity.CreatedAt,
            entity.MasterVocabulary?.PhoneticUk, entity.MasterVocabulary?.PhoneticUs,
            entity.MasterVocabulary?.AudioUrl, entity.MasterVocabulary?.PartOfSpeech);

        var options = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(1) };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dto), options, ct);

        return Result<VocabularyDto>.Success(dto);
    }
}

// ─── Get Vocabulary Stats ───────────────────────────────────────
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
