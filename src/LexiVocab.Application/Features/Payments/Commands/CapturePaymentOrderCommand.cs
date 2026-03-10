using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using MediatR;

namespace LexiVocab.Application.Features.Payments.Commands;

public record CapturePaymentOrderCommand(string OrderId) : IRequest<Result<string>>;

public class CapturePaymentOrderHandler : IRequestHandler<CapturePaymentOrderCommand, Result<string>>
{
    private readonly IPaymentServiceFactory _paymentFactory;
    private readonly ICurrentUserService _currentUser;

    public CapturePaymentOrderHandler(IPaymentServiceFactory paymentFactory, ICurrentUserService currentUser)
    {
        _paymentFactory = paymentFactory;
        _currentUser = currentUser;
    }

    public async Task<Result<string>> Handle(CapturePaymentOrderCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var paypalService = _paymentFactory.GetService(PaymentProvider.PayPal);
        var success = await paypalService.CaptureOrderAsync(request.OrderId, userId, ct);

        return success
            ? Result<string>.Success("Payment successful. Account upgraded.")
            : Result<string>.Failure("Failed to capture payment or order already processed.", 400);
    }
}
