using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Payment;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Payments.Queries;

public record GetPaymentHistoryQuery(int Page = 1, int PageSize = 20) 
    : IRequest<Result<PagedResult<PaymentHistoryDto>>>;

public class GetPaymentHistoryHandler : IRequestHandler<GetPaymentHistoryQuery, Result<PagedResult<PaymentHistoryDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IPaymentServiceFactory _paymentFactory;

    public GetPaymentHistoryHandler(IUnitOfWork uow, ICurrentUserService currentUser, IPaymentServiceFactory paymentFactory)
    {
        _uow = uow;
        _currentUser = currentUser;
        _paymentFactory = paymentFactory;
    }

    public async Task<Result<PagedResult<PaymentHistoryDto>>> Handle(GetPaymentHistoryQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        // Lazy-expire: flip expired pending transactions before fetching
        await ExpirePendingTransactionsAsync(userId, ct);

        var (transactions, totalCount) = await _uow.PaymentTransactions
            .GetPaginatedByUserAsync(userId, request.Page, request.PageSize, ct);

        var items = transactions.Select(t => {
            string? approvalUrl = null;
            if (t.Status == PaymentStatus.Pending)
            {
                var service = _paymentFactory.GetService(t.Provider);
                approvalUrl = service.GetApprovalUrl(t.ExternalOrderId, t.Amount);
            }

            return new PaymentHistoryDto(
                t.Id,
                t.Provider.ToString(),
                t.ExternalOrderId,
                t.Amount,
                t.Currency,
                t.Status.ToString(),
                t.CreatedAt,
                t.PaidAt,
                t.ExpiresAt,
                t.CancelledAt,
                approvalUrl,
                t.Coupon != null ? t.Amount + (t.Coupon.DiscountType == DiscountType.FixedAmount ? t.Coupon.DiscountValue : Math.Round(t.Amount / (1 - t.Coupon.DiscountValue / 100m) - t.Amount, 2)) : null,
                t.Coupon != null ? (t.Coupon.DiscountType == DiscountType.FixedAmount ? t.Coupon.DiscountValue : Math.Round(t.Amount / (1 - t.Coupon.DiscountValue / 100m) - t.Amount, 2)) : null,
                t.Coupon?.Code,
                t.Subscription?.PlanDefinition?.Name);
        }).ToList();

        return Result<PagedResult<PaymentHistoryDto>>.Success(new PagedResult<PaymentHistoryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    /// <summary>
    /// Finds tracked pending expired transactions for the user and flips them to Expired.
    /// </summary>
    private async Task ExpirePendingTransactionsAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var expiredTransactions = await _uow.PaymentTransactions
            .GetExpiredPendingByUserAsync(userId, now, ct);

        if (expiredTransactions.Count == 0) return;

        foreach (var tx in expiredTransactions)
        {
            PaymentExpirationHelper.ExpireIfNeeded(tx, now);
        }

        await _uow.SaveChangesAsync(ct);
    }
}
