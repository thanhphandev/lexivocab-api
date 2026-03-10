using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.MasterVocabulary;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Application.Common.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.MasterVocabularies.Queries;

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
