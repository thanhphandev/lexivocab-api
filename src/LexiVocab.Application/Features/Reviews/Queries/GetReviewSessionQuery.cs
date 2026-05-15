using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Review;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Domain.Enums;
using MediatR;

namespace LexiVocab.Application.Features.Reviews.Queries;

/// <summary>
/// Fetches cards where NextReviewDate <= NOW() and IsArchived == false.
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
        var userId = _currentUser.GetRequiredUserId();
        
        // Fetch user settings to enforce limits
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        var maxReviewLimit = user?.UserSetting?.DailyReviewLimit ?? 100;
        var maxNewCardLimit = user?.UserSetting?.DailyNewCardLimit ?? 20;
        
        // Protect user from "Review Hell" by capping the limit. 
        // We use Math.Min against request.Limit dynamically if the UI requested a smaller batch.
        var actualReviewLimit = Math.Min(request.Limit, maxReviewLimit);
        var actualNewCardLimit = Math.Min(request.Limit, maxNewCardLimit);

        var dueItems = await _uow.Vocabularies.GetDueForReviewAsync(userId, actualReviewLimit, actualNewCardLimit, ct);

        if (dueItems.Count == 0)
        {
            return Result<ReviewSessionDto>.NotFound("No cards due for review at this time.", ErrorCode.REVIEW_NO_CARDS_DUE);
        }

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
