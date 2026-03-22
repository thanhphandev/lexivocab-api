using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Payments.Commands;

/// <summary>
/// Allows the currently authenticated user to cancel their own active subscription.
/// Sets status to Cancelled and EndDate to now (access revoked immediately).
/// </summary>
public record CancelMySubscriptionCommand : IRequest<Result>;

public class CancelMySubscriptionHandler : IRequestHandler<CancelMySubscriptionCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CancelMySubscriptionHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(CancelMySubscriptionCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return Result.NotFound("User not found in context.", ErrorCode.RESOURCE_NOT_FOUND);

        var activeSub = await _uow.Subscriptions.GetActiveByUserIdAsync(userId.Value, ct);
        if (activeSub == null)
        {
            var subs = await _uow.Subscriptions.GetByUserIdAsync(userId.Value, ct);
            if (subs.Any())
            {
                var latest = subs.OrderByDescending(s => s.CreatedAt).First();
                if (latest.Status == SubscriptionStatus.Expired || (latest.EndDate.HasValue && latest.EndDate < DateTime.UtcNow))
                    return Result.Failure("Subscription is already expired.", 400, ErrorCode.SUB_EXPIRED);
                if (latest.Status == SubscriptionStatus.Cancelled)
                    return Result.Failure("Subscription is already cancelled.", 400, ErrorCode.SUB_CANCELLED);
            }
            return Result.Failure("No active subscription found.", 404, ErrorCode.SUB_NOT_FOUND);
        }

        activeSub.Status = SubscriptionStatus.Cancelled;
        activeSub.EndDate = DateTime.UtcNow;
        activeSub.UpdatedAt = DateTime.UtcNow;
        _uow.Subscriptions.Update(activeSub);

        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
