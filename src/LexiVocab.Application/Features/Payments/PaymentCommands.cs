using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Features.Payments;

// ─── Create Payment Order ──────────────────────────────────────────
public record CreatePaymentOrderCommand(string PlanId, PaymentProvider Provider) : IRequest<Result<string>>;

public class CreatePaymentOrderHandler : IRequestHandler<CreatePaymentOrderCommand, Result<string>>
{
    private readonly IPaymentServiceFactory _paymentFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CreatePaymentOrderHandler> _logger;

    public CreatePaymentOrderHandler(IPaymentServiceFactory paymentFactory, ICurrentUserService currentUser, ILogger<CreatePaymentOrderHandler> logger)
    {
        _paymentFactory = paymentFactory;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(CreatePaymentOrderCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        try
        {
            var paymentService = _paymentFactory.GetService(request.Provider);
            var approvalUrl = await paymentService.CreateOrderAsync(userId, request.PlanId, ct);
            return Result<string>.Success(approvalUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create payment order for user {UserId} using {Provider}.", userId, request.Provider);
            return Result<string>.Failure("Payment gateway error. Please try again later.", 500);
        }
    }
}

// ─── Capture PayPal Order ─────────────────────────────────────────
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
        // Search through all services that support "Capture" (PayPal)
        // Or we could pass the provider to the capture command too.
        // For now, most captures are PayPal.
        var paypalService = _paymentFactory.GetService(PaymentProvider.PayPal);
        var success = await paypalService.CaptureOrderAsync(request.OrderId, userId, ct);

        return success
            ? Result<string>.Success("Payment successful. Account upgraded.")
            : Result<string>.Failure("Failed to capture payment or order already processed.", 400);
    }
}

// ─── Process Payment Webhook ─────────────────────────────────────
public record ProcessPaymentWebhookCommand(PaymentProvider Provider, string Body, IDictionary<string, string> Headers) : IRequest<Result<Unit>>;

public class ProcessPaymentWebhookHandler : IRequestHandler<ProcessPaymentWebhookCommand, Result<Unit>>
{
    private readonly IPaymentServiceFactory _paymentFactory;
    private readonly ILogger<ProcessPaymentWebhookHandler> _logger;

    public ProcessPaymentWebhookHandler(IPaymentServiceFactory paymentFactory, ILogger<ProcessPaymentWebhookHandler> logger)
    {
        _paymentFactory = paymentFactory;
        _logger = logger;
    }

    public async Task<Result<Unit>> Handle(ProcessPaymentWebhookCommand request, CancellationToken ct)
    {
        var service = _paymentFactory.GetService(request.Provider);
        
        var isValid = await service.VerifyWebhookSignatureAsync(request.Body, request.Headers);
        if (!isValid)
        {
            _logger.LogWarning("{Provider} Webhook Verification Failed.", request.Provider);
            return Result<Unit>.Failure("Invalid webhook signature.", 401);
        }

        await service.ProcessWebhookEventAsync(request.Body, ct);
        return Result<Unit>.Success(Unit.Value);
    }
}
