using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace LexiVocab.Application.Features.Vocabularies.Queries;

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
