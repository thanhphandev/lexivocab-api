using LexiVocab.Application.Common;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Commands;

public record CancelCurrentSubscriptionCommand(Guid UserId) : IRequest<Result<string>>;

public class CancelCurrentSubscriptionHandler : IRequestHandler<CancelCurrentSubscriptionCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;

    public CancelCurrentSubscriptionHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<string>> Handle(CancelCurrentSubscriptionCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
        if (user == null) return Result<string>.Failure("User not found", 404);

        var currentSub = await _uow.Subscriptions.GetActiveByUserIdAsync(user.Id, ct);
        if (currentSub == null) return Result<string>.Failure("User has no active subscriptions", 400);

        currentSub.Status = SubscriptionStatus.Cancelled;
        currentSub.EndDate = DateTime.UtcNow;
        currentSub.UpdatedAt = DateTime.UtcNow;
        _uow.Subscriptions.Update(currentSub);

        user.UpdatedAt = DateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result<string>.Success("Subscription cancelled.");
    }
}
