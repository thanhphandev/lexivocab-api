using LexiVocab.Application.Common;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Commands;

// ─── Update User Role ─────────────────────────────────────────────
public record UpdateUserRoleCommand(Guid UserId, string Role) : IRequest<Result<string>>;

public class UpdateUserRoleHandler : IRequestHandler<UpdateUserRoleCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;

    public UpdateUserRoleHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<string>> Handle(UpdateUserRoleCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
        if (user == null) return Result<string>.Failure("User not found", 404);

        if (!Enum.TryParse<UserRole>(request.Role, true, out var roleEnum))
        {
            return Result<string>.Failure("Invalid role. Must be 'User', 'Premium', or 'Admin'.", 400);
        }

        user.Role = roleEnum;
        user.UpdatedAt = DateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result<string>.Success($"User role updated to {roleEnum}");
    }
}

// ─── Update User Status ───────────────────────────────────────────
public record UpdateUserStatusCommand(Guid UserId, bool IsActive) : IRequest<Result<string>>;

public class UpdateUserStatusHandler : IRequestHandler<UpdateUserStatusCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;

    public UpdateUserStatusHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<string>> Handle(UpdateUserStatusCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
        if (user == null) return Result<string>.Failure("User not found", 404);

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        var statusString = request.IsActive ? "Activated" : "Deactivated";
        return Result<string>.Success($"User successfully {statusString}");
    }
}

// ─── Add Manual Subscription ──────────────────────────────────────
public record AddManualSubscriptionCommand(Guid UserId, string Plan, int DurationDays) : IRequest<Result<string>>;

public class AddManualSubscriptionHandler : IRequestHandler<AddManualSubscriptionCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;

    public AddManualSubscriptionHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<string>> Handle(AddManualSubscriptionCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
        if (user == null) return Result<string>.Failure("User not found", 404);

        if (!Enum.TryParse<SubscriptionPlan>(request.Plan, true, out var planEnum))
            return Result<string>.Failure("Invalid plan. Use 'Premium' or 'Free'.", 400);

        // Deactivate existing active subscription if any
        var currentSub = await _uow.Subscriptions.GetActiveByUserIdAsync(user.Id, ct);
        if (currentSub != null)
        {
            currentSub.Status = SubscriptionStatus.Cancelled;
            currentSub.EndDate = DateTime.UtcNow;
            currentSub.UpdatedAt = DateTime.UtcNow;
            _uow.Subscriptions.Update(currentSub);
        }

        var endDate = DateTime.UtcNow.AddDays(request.DurationDays);

        var newSub = new Subscription
        {
            UserId = user.Id,
            Plan = planEnum,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow,
            EndDate = endDate,
            Provider = PaymentProvider.Mock,
            ExternalSubscriptionId = $"MANUAL-{Guid.NewGuid().ToString()[..8]}"
        };

        if (planEnum == SubscriptionPlan.Premium && user.Role != UserRole.Admin)
        {
            user.Role = UserRole.Premium;
        }

        // Update denormalized cache
        user.PlanExpirationDate = endDate;
        user.UpdatedAt = DateTime.UtcNow;

        await _uow.Subscriptions.AddAsync(newSub, ct);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result<string>.Success($"Added {request.DurationDays}-day {planEnum} subscription.");
    }
}

// ─── Cancel Current Subscription ──────────────────────────────────
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

        if (user.Role == UserRole.Premium)
        {
            user.Role = UserRole.User; // fallback
        }
        user.PlanExpirationDate = null;
        user.UpdatedAt = DateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result<string>.Success("Subscription cancelled.");
    }
}
