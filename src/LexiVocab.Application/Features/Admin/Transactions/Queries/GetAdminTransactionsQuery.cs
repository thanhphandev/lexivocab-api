using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Transactions.Queries;

public record GetAdminTransactionsQuery(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Status = null,
    string? Provider = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<AdminTransactionDto>>>;

public class GetAdminTransactionsHandler : IRequestHandler<GetAdminTransactionsQuery, Result<PagedResult<AdminTransactionDto>>>
{
    private readonly IUnitOfWork _uow;

    public GetAdminTransactionsHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<PagedResult<AdminTransactionDto>>> Handle(GetAdminTransactionsQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await _uow.PaymentTransactions.GetPagedForAdminAsync(
            request.FromDate, request.ToDate, request.Status, request.Provider, request.Search,
            request.Page, request.PageSize, ct);

        var dtos = items.Select(t => new AdminTransactionDto(
            t.Id,
            t.ExternalOrderId,
            t.Amount,
            t.Currency,
            t.Status.ToString(),
            t.Provider.ToString(),
            t.CreatedAt,
            t.PaidAt,
            t.CancelledAt,
            t.CancelReason,
            t.UserId,
            t.User?.Email ?? "Unknown",
            t.SubscriptionId,
            t.Subscription?.PlanDefinition?.Name ?? "Unknown",
            t.CouponId,
            t.Coupon?.Code
        )).ToList();

        var pagedResult = new PagedResult<AdminTransactionDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<AdminTransactionDto>>.Success(pagedResult);
    }
}
