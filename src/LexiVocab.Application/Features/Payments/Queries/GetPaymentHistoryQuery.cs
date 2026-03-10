using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Payment;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Payments.Queries;

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
