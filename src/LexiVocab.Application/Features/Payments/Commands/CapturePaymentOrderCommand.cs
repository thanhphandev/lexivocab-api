using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Payments.Commands;

public record CapturePaymentOrderCommand(string OrderId) : IRequest<Result<string>>;

public class CapturePaymentOrderHandler : IRequestHandler<CapturePaymentOrderCommand, Result<string>>
{
    private readonly IPaymentServiceFactory _paymentFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public CapturePaymentOrderHandler(IPaymentServiceFactory paymentFactory, ICurrentUserService currentUser, IUnitOfWork uow)
    {
        _paymentFactory = paymentFactory;
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<string>> Handle(CapturePaymentOrderCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var tx = await _uow.PaymentTransactions.GetByExternalOrderIdAsync(request.OrderId, ct);
        if (tx == null)
            return Result<string>.NotFound("Payment order not found.", ErrorCode.PAYMENT_ORDER_NOT_FOUND);

        if (tx.Status == PaymentStatus.Expired)
            return Result<string>.Failure("Payment order has expired.", 400, ErrorCode.PAYMENT_ORDER_EXPIRED);
        if (tx.Status == PaymentStatus.Completed)
            return Result<string>.Failure("Payment order already processed.", 400, ErrorCode.PAYMENT_ALREADY_PROCESSED);
        if (tx.Status == PaymentStatus.Failed || tx.Status == PaymentStatus.Cancelled || tx.Status == PaymentStatus.Refunded)
            return Result<string>.Failure("Payment declined or cancelled.", 402, ErrorCode.PAYMENT_DECLINED);

        // Use the provider from the transaction instead of hardcoding PayPal
        var paymentService = _paymentFactory.GetService(tx.Provider);
        var success = await paymentService.CaptureOrderAsync(request.OrderId, userId, ct);

        return success
            ? Result<string>.Success("Payment successful. Account upgraded.")
            : Result<string>.Failure("Failed to capture payment.", 402, ErrorCode.PAYMENT_DECLINED);
    }
}
