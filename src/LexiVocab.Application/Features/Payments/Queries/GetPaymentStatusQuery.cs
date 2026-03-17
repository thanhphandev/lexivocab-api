using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Payment;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Payments.Queries;

public record GetPaymentStatusQuery(string Reference) : IRequest<Result<PaymentStatusDto>>;

public class GetPaymentStatusHandler : IRequestHandler<GetPaymentStatusQuery, Result<PaymentStatusDto>>
{
    private readonly IUnitOfWork _uow;

    public GetPaymentStatusHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<PaymentStatusDto>> Handle(GetPaymentStatusQuery request, CancellationToken ct)
    {
        var tx = await _uow.PaymentTransactions
            .GetByExternalOrderIdAsync(request.Reference, ct);

        if (tx == null) return Result<PaymentStatusDto>.NotFound("Transaction not found.");

        // If still pending but already expired, flip it server-side immediately.
        var now = DateTime.UtcNow;
        if (PaymentExpirationHelper.ExpireIfNeeded(tx, now))
        {
            await _uow.SaveChangesAsync(ct);
        }

        return Result<PaymentStatusDto>.Success(new PaymentStatusDto(tx.Status.ToString(), tx.ExpiresAt));
    }
}
