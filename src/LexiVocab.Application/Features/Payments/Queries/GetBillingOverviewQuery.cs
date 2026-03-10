using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Payment;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Payments.Queries;

public record GetBillingOverviewQuery() : IRequest<Result<BillingOverviewDto>>;

public class GetBillingOverviewHandler : IRequestHandler<GetBillingOverviewQuery, Result<BillingOverviewDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFeatureGatingService _featureGating;

    public GetBillingOverviewHandler(IUnitOfWork uow, ICurrentUserService currentUser, IFeatureGatingService featureGating)
    {
        _uow = uow;
        _currentUser = currentUser;
        _featureGating = featureGating;
    }

    public async Task<Result<BillingOverviewDto>> Handle(GetBillingOverviewQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        
        var permissions = await _featureGating.GetPermissionsAsync(userId, ct);

        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user == null) return Result<BillingOverviewDto>.NotFound("User not found.");

        var activeSub = await _uow.Subscriptions.GetActiveByUserIdAsync(userId, ct);

        var totalTx = await _uow.PaymentTransactions.CountByUserAsync(userId, ct);

        SubscriptionDto? subDto = activeSub != null
            ? new SubscriptionDto(
                activeSub.Id,
                activeSub.PlanDefinition.Name,
                activeSub.Status.ToString(),
                activeSub.StartDate,
                activeSub.EndDate,
                activeSub.Provider.ToString(),
                activeSub.ExternalSubscriptionId)
            : null;

        return Result<BillingOverviewDto>.Success(new BillingOverviewDto(
            ActiveSubscription: subDto,
            IsPremium: permissions.Plan != "Free" && permissions.Plan != "None",
            Plan: permissions.Plan,
            PlanExpiresAt: permissions.PlanExpiresAt,
            TotalTransactions: totalTx));
    }
}
