using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace LexiVocab.Application.Features.Vocabularies.Queries;

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
