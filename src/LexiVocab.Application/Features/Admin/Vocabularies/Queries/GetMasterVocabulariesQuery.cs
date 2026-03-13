using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Vocabularies.Queries;

public record GetMasterVocabulariesQuery(int Page, int PageSize, string? SearchQuery) : IRequest<Result<PagedResult<MasterVocabularyDto>>>;

public class GetMasterVocabulariesHandler : IRequestHandler<GetMasterVocabulariesQuery, Result<PagedResult<MasterVocabularyDto>>>
{
    private readonly IUnitOfWork _uow;

    public GetMasterVocabulariesHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<PagedResult<MasterVocabularyDto>>> Handle(GetMasterVocabulariesQuery request, CancellationToken ct)
    {
        var (items, totalItems) = await _uow.MasterVocabularies.GetPagedAsync(
            request.Page, request.PageSize, request.SearchQuery, ct);

        var dtos = items.Select(v => new MasterVocabularyDto(
            v.Id,
            v.Word,
            v.PartOfSpeech,
            v.PhoneticUk,
            v.PhoneticUs,
            v.AudioUrl,
            v.PopularityRank,
            v.CreatedAt,
            v.UpdatedAt)).ToList();

        var pagedResult = new PagedResult<MasterVocabularyDto>
        {
            Items = dtos,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalItems
        };

        return Result<PagedResult<MasterVocabularyDto>>.Success(pagedResult);
    }
}
