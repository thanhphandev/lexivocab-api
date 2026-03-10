using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.MasterVocabulary;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Application.Common.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.MasterVocabularies.Queries;

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
