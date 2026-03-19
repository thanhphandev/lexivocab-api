using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.MasterVocabulary;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Vocabularies.Queries;

public record ExploreVocabulariesQuery(int Page, int PageSize, string? Search = null) : IRequest<Result<PagedResult<MasterVocabularyDto>>>;

public class ExploreVocabulariesHandler : IRequestHandler<ExploreVocabulariesQuery, Result<PagedResult<MasterVocabularyDto>>>
{
    private readonly IUnitOfWork _uow;

    public ExploreVocabulariesHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<PagedResult<MasterVocabularyDto>>> Handle(ExploreVocabulariesQuery request, CancellationToken ct)
    {
        var result = await _uow.MasterVocabularies.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            isApproved: true,
            ct);

        var dtos = result.Items.Select(m => new MasterVocabularyDto(
            m.Id,
            m.Word,
            m.PartOfSpeech,
            m.PhoneticUk,
            m.PhoneticUs,
            m.AudioUrl,
            m.PopularityRank,
            m.Meaning,
            m.CefrLevel
        )).ToList();

        return Result<PagedResult<MasterVocabularyDto>>.Success(
            new PagedResult<MasterVocabularyDto>(dtos, result.TotalCount, result.Page, result.PageSize));
    }
}
