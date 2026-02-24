using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Review;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Reviews.Queries;

// ─── Get Today's Review Session ─────────────────────────────────
/// <summary>
/// Fetches cards where NextReviewDate &lt;= NOW() and IsArchived == false.
/// This is the critical sub-millisecond indexed query.
/// </summary>
public record GetReviewSessionQuery(int Limit = 50)
    : IRequest<Result<ReviewSessionDto>>;

public class GetReviewSessionHandler : IRequestHandler<GetReviewSessionQuery, Result<ReviewSessionDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetReviewSessionHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<ReviewSessionDto>> Handle(GetReviewSessionQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var dueItems = await _uow.Vocabularies.GetDueForReviewAsync(userId, request.Limit, ct);

        var cards = dueItems.Select(v => new ReviewCardDto(
            v.Id,
            v.WordText,
            v.CustomMeaning,
            v.ContextSentence,
            v.MasterVocabulary?.PhoneticUs,
            v.MasterVocabulary?.AudioUrl,
            v.RepetitionCount,
            v.EasinessFactor
        )).ToList();

        // Get total due count for progress display
        var (_, _, _, totalDue) = await _uow.Vocabularies.GetStatsAsync(userId, ct);

        return Result<ReviewSessionDto>.Success(new ReviewSessionDto(cards, totalDue));
    }
}

// ─── Get Review History ─────────────────────────────────────────
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
        // For now, return empty — will be enhanced with full pagination later
        return Result<PagedResult<ReviewHistoryDto>>.Success(new PagedResult<ReviewHistoryDto>
        {
            Items = [],
            TotalCount = 0,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}
