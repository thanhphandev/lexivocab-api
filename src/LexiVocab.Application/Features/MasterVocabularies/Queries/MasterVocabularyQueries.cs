using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.MasterVocabulary;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.MasterVocabularies.Queries;

using LexiVocab.Application.Common.Interfaces;

// ─── Lookup Word in Master Dictionary ───────────────────────────
public record LookupMasterVocabQuery(string Word)
    : IRequest<Result<MasterVocabularyDto>>, ICacheableQuery<Result<MasterVocabularyDto>>
{
    public string CacheKey => $"MasterVocab_{Word.ToLowerInvariant().Trim()}";
    public TimeSpan? CacheDuration => TimeSpan.FromDays(7);
}

public class LookupMasterVocabHandler : IRequestHandler<LookupMasterVocabQuery, Result<MasterVocabularyDto>>
{
    private readonly IUnitOfWork _uow;

    public LookupMasterVocabHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<MasterVocabularyDto>> Handle(LookupMasterVocabQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Word))
            return Result<MasterVocabularyDto>.Failure("Word parameter is required.");

        var result = await _uow.MasterVocabularies.GetByWordAsync(
            request.Word.ToLowerInvariant().Trim(), ct);

        if (result is null)
            return Result<MasterVocabularyDto>.NotFound(
                $"Word '{request.Word}' not found in master dictionary.");

        return Result<MasterVocabularyDto>.Success(new MasterVocabularyDto(
            result.Id, result.Word, result.PartOfSpeech,
            result.PhoneticUk, result.PhoneticUs, result.AudioUrl,
            result.PopularityRank));
    }
}

// ─── Search Master Dictionary (Autocomplete) ────────────────────
public record SearchMasterVocabQuery(string Query, int Limit = 10)
    : IRequest<Result<IReadOnlyList<MasterVocabularySearchResultDto>>>, ICacheableQuery<Result<IReadOnlyList<MasterVocabularySearchResultDto>>>
{
    public string CacheKey => $"SearchVocab_{Query?.ToLowerInvariant().Trim()}_{Limit}";
    public TimeSpan? CacheDuration => TimeSpan.FromHours(24);
}

public class SearchMasterVocabHandler
    : IRequestHandler<SearchMasterVocabQuery, Result<IReadOnlyList<MasterVocabularySearchResultDto>>>
{
    private readonly IUnitOfWork _uow;

    public SearchMasterVocabHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<IReadOnlyList<MasterVocabularySearchResultDto>>> Handle(
        SearchMasterVocabQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return Result<IReadOnlyList<MasterVocabularySearchResultDto>>.Failure(
                "Query parameter 'q' is required.");

        var results = await _uow.MasterVocabularies.SearchAsync(
            request.Query.ToLowerInvariant().Trim(), request.Limit, ct);

        var dtos = results.Select(r => new MasterVocabularySearchResultDto(
            r.Word, r.PartOfSpeech, r.PhoneticUs, r.PopularityRank))
            .ToList();

        return Result<IReadOnlyList<MasterVocabularySearchResultDto>>.Success(dtos);
    }
}
