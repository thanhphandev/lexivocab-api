using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Tag;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Tags.Queries;

public record GetTagVocabulariesQuery(
    Guid TagId, 
    int Page = 1, 
    int PageSize = 20
) : IRequest<Result<PagedResult<VocabularyDto>>>;

public class GetTagVocabulariesHandler : IRequestHandler<GetTagVocabulariesQuery, Result<PagedResult<VocabularyDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetTagVocabulariesHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<VocabularyDto>>> Handle(GetTagVocabulariesQuery request, CancellationToken ct)
    {
        var tag = await _uow.Tags.GetByIdAsync(request.TagId, ct);
        if (tag is null || tag.UserId != _currentUser.UserId)
            return Result<PagedResult<VocabularyDto>>.NotFound("Tag not found.");

        var (items, totalCount) = await _uow.Vocabularies.GetByTagIdAsync(
            _currentUser.UserId.Value, request.TagId, request.Page, request.PageSize, ct);

        var dtos = items.Select(v => new VocabularyDto(
            v.Id, v.TagId, v.WordText, v.CustomMeaning, v.ContextSentence, v.SourceUrl,
            v.RepetitionCount, v.EasinessFactor, v.IntervalDays,
            v.NextReviewDate, v.LastReviewedAt, v.IsArchived, v.CreatedAt,
            v.MasterVocabulary?.PhoneticUk, v.MasterVocabulary?.PhoneticUs,
            v.MasterVocabulary?.AudioUrl, v.MasterVocabulary?.PartOfSpeech
        )).ToList();

        var result = new PagedResult<VocabularyDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<VocabularyDto>>.Success(result);
    }
}
