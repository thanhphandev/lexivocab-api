using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Commands;

public record AddManualSubscriptionCommand(Guid UserId, string PlanName, int DurationDays) : IRequest<Result<string>>;

public class AddManualSubscriptionHandler : IRequestHandler<AddManualSubscriptionCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _dateTime;

    public AddManualSubscriptionHandler(IUnitOfWork uow, IDateTimeProvider dateTime)
    {
        _uow = uow;
        _dateTime = dateTime;
    }

    public async Task<Result<string>> Handle(AddManualSubscriptionCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
        if (user == null) return Result<string>.Failure("User not found", 404);

        var plan = await _uow.PlanDefinitions.GetByNameAsync(request.PlanName, ct);
            
        if (plan == null)
            return Result<string>.Failure($"Plan '{request.PlanName}' not found.", 404);

        var currentSub = await _uow.Subscriptions.GetActiveByUserIdAsync(user.Id, ct);
        if (currentSub != null)
        {
            currentSub.Status = SubscriptionStatus.Cancelled;
            currentSub.EndDate = _dateTime.UtcNow;
            currentSub.UpdatedAt = _dateTime.UtcNow;
            _uow.Subscriptions.Update(currentSub);
        }

        var now = _dateTime.UtcNow;
        var endDate = now.AddDays(request.DurationDays);

        var newSub = new Subscription
        {
            UserId = user.Id,
            PlanDefinitionId = plan.Id,
            Status = SubscriptionStatus.Active,
            StartDate = now,
            EndDate = endDate,
            Provider = PaymentProvider.Mock,
            ExternalSubscriptionId = $"MANUAL-{Guid.NewGuid().ToString()[..8]}"
        };

        user.UpdatedAt = now;

        await _uow.Subscriptions.AddAsync(newSub, ct);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result<string>.Success($"Added {request.DurationDays}-day {plan.Name} subscription.");
    }
}
