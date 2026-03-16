using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Review;
using LexiVocab.Domain.Interfaces;
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
        var userId = _currentUser.UserId!.Value;
        
        // Fetch user settings to enforce limits
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        var maxReviewLimit = user?.UserSetting?.DailyReviewLimit ?? 100;
        
        // Protect user from "Review Hell" by capping the limit
        var actualLimit = Math.Min(request.Limit, maxReviewLimit);

        var dueItems = await _uow.Vocabularies.GetDueForReviewAsync(userId, actualLimit, ct);

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
