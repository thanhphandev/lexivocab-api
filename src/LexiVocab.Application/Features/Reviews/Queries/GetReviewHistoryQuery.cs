using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Review;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Reviews.Queries;

public record GetReviewHistoryQuery(
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<ReviewHistoryDto>>>;

public class GetReviewHistoryHandler : IRequestHandler<GetReviewHistoryQuery, Result<PagedResult<ReviewHistoryDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetReviewHistoryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<ReviewHistoryDto>>> Handle(GetReviewHistoryQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var (items, totalCount) = await _uow.ReviewLogs.GetPaginatedByUserAsync(
            userId, request.Page, request.PageSize, ct);

        var dtos = items.Select(r => new ReviewHistoryDto(
            r.Id,
            r.UserVocabularyId,
            r.UserVocabulary?.WordText ?? "Unknown",
            r.QualityScore,
            r.TimeSpentMs,
            r.ReviewedAt)).ToList();

        return Result<PagedResult<ReviewHistoryDto>>.Success(new PagedResult<ReviewHistoryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}
