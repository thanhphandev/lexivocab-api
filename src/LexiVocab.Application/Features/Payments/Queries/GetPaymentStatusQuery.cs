using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Payment;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace LexiVocab.Application.Features.Payments.Queries;

public record GetPaymentStatusQuery(string Reference) : IRequest<Result<PaymentStatusDto>>;

public class GetPaymentStatusHandler : IRequestHandler<GetPaymentStatusQuery, Result<PaymentStatusDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;

    public GetPaymentStatusHandler(IUnitOfWork uow, IConfiguration config)
    {
        _uow = uow;
        _config = config;
    }

    public async Task<Result<PaymentStatusDto>> Handle(GetPaymentStatusQuery request, CancellationToken ct)
    {
        var tx = await _uow.PaymentTransactions
            .GetByExternalOrderIdAsync(request.Reference, ct);

        if (tx == null) return Result<PaymentStatusDto>.NotFound("Transaction not found.");

        // If still pending but already expired, flip it server-side immediately.
        if (tx.Status == LexiVocab.Domain.Enums.PaymentStatus.Pending)
        {
            var now = DateTime.UtcNow;

            var isExpired =
                (tx.ExpiresAt.HasValue && tx.ExpiresAt.Value <= now);

            if (isExpired)
            {
                tx.Status = LexiVocab.Domain.Enums.PaymentStatus.Expired;
                tx.CancelledAt = now;
                tx.CancelReason = "Expired by configured pending payment expiry.";

                if (tx.Subscription != null && tx.Subscription.Status == LexiVocab.Domain.Enums.SubscriptionStatus.Pending)
                    tx.Subscription.Status = LexiVocab.Domain.Enums.SubscriptionStatus.Cancelled;

                await _uow.SaveChangesAsync(ct);
            }
        }

        return Result<PaymentStatusDto>.Success(new PaymentStatusDto(tx.Status.ToString(), tx.ExpiresAt));
    }
}
