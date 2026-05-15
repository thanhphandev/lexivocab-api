using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Common.Mappings;
using LexiVocab.Application.DTOs.Tag;
using LexiVocab.Application.DTOs.Vocabulary;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Domain.Enums;
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
        var userId = _currentUser.GetRequiredUserId();

        var tag = await _uow.Tags.GetByIdAsync(request.TagId, ct);
        if (tag is null || tag.UserId != userId)
            return Result<PagedResult<VocabularyDto>>.NotFound("Tag not found.", ErrorCode.TAG_NOT_FOUND);

        var (items, totalCount) = await _uow.Vocabularies.GetByTagIdAsync(
            userId, request.TagId, request.Page, request.PageSize, ct);

        var dtos = items.Select(v => v.MapToDto()).ToList();

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
