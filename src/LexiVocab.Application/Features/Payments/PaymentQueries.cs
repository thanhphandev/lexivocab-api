using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Payment;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Payments;

// ─── Get Billing Overview ─────────────────────────────────────────
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
        var isPremium = await _featureGating.IsPremiumAsync(userId, ct);

        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user == null) return Result<BillingOverviewDto>.NotFound("User not found.");

        var activeSub = await _uow.Subscriptions.GetActiveByUserIdAsync(userId, ct);

        var totalTx = await _uow.PaymentTransactions.CountByUserAsync(userId, ct);

        SubscriptionDto? subDto = activeSub != null
            ? new SubscriptionDto(
                activeSub.Id,
                activeSub.Plan.ToString(),
                activeSub.Status.ToString(),
                activeSub.StartDate,
                activeSub.EndDate,
                activeSub.Provider.ToString(),
                activeSub.ExternalSubscriptionId)
            : null;

        return Result<BillingOverviewDto>.Success(new BillingOverviewDto(
            ActiveSubscription: subDto,
            IsPremium: isPremium,
            Plan: isPremium ? "Premium" : "Free",
            PlanExpiresAt: user.PlanExpirationDate,
            TotalTransactions: totalTx));
    }
}

// ─── Get Payment History ──────────────────────────────────────────
public record GetPaymentHistoryQuery(int Page = 1, int PageSize = 20) 
    : IRequest<Result<PagedResult<PaymentHistoryDto>>>;

public class GetPaymentHistoryHandler : IRequestHandler<GetPaymentHistoryQuery, Result<PagedResult<PaymentHistoryDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetPaymentHistoryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<PaymentHistoryDto>>> Handle(GetPaymentHistoryQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var (transactions, totalCount) = await _uow.PaymentTransactions
            .GetPaginatedByUserAsync(userId, request.Page, request.PageSize, ct);

        var items = transactions.Select(t => new PaymentHistoryDto(
            t.Id,
            t.Provider.ToString(),
            t.ExternalOrderId,
            t.Amount,
            t.Currency,
            t.Status.ToString(),
            t.CreatedAt,
            t.PaidAt)).ToList();

        return Result<PagedResult<PaymentHistoryDto>>.Success(new PagedResult<PaymentHistoryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}
