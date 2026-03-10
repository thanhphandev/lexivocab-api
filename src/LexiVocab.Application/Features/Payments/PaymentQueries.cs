using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Payment;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Domain.Enums;
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
        
        // Use FeatureGating to get structured permissions
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

// ─── Get Subscription Plans ───────────────────────────────────────
public record GetSubscriptionPlansQuery() : IRequest<Result<List<SubscriptionPlanDto>>>;

public class GetSubscriptionPlansHandler : IRequestHandler<GetSubscriptionPlansQuery, Result<List<SubscriptionPlanDto>>>
{
    private readonly IUnitOfWork _uow;

    public GetSubscriptionPlansHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<List<SubscriptionPlanDto>>> Handle(GetSubscriptionPlansQuery request, CancellationToken ct)
    {
        var plans = await _uow.PlanDefinitions.GetAllWithFeaturesAsync(ct);

        var dtos = plans.Select(p => new SubscriptionPlanDto(
            p.Id.ToString(),
            p.NameKey,
            p.Price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            p.DurationDays switch { 30 => "monthly", 365 => "yearly", _ => "one_time" },
            p.Description,
            p.IsRecommended,
            p.PlanFeatures.Select(f => new PlanFeatureDto(
                $"{f.Feature.Name}: {f.Value}", 
                !f.Value.Equals("false", StringComparison.OrdinalIgnoreCase))
            ).ToList()
        )).ToList();

        return Result<List<SubscriptionPlanDto>>.Success(dtos);
    }
}

// ─── Get Payment Status ───────────────────────────────────────────
public record GetPaymentStatusQuery(string Reference) : IRequest<Result<string>>;

public class GetPaymentStatusHandler : IRequestHandler<GetPaymentStatusQuery, Result<string>>
{
    private readonly IUnitOfWork _uow;

    public GetPaymentStatusHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<string>> Handle(GetPaymentStatusQuery request, CancellationToken ct)
    {
        var tx = await _uow.PaymentTransactions
            .GetByExternalOrderIdAsync(request.Reference, ct);

        if (tx == null) return Result<string>.NotFound("Transaction not found.");

        return Result<string>.Success(tx.Status.ToString());
    }
}
