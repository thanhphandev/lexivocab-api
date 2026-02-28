using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Features.Payments;

// ─── Create PayPal Order ──────────────────────────────────────────
public record CreatePaymentOrderCommand(string PlanId) : IRequest<Result<string>>;

public class CreatePaymentOrderHandler : IRequestHandler<CreatePaymentOrderCommand, Result<string>>
{
    private readonly IPaymentService _paymentService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CreatePaymentOrderHandler> _logger;

    public CreatePaymentOrderHandler(IPaymentService paymentService, ICurrentUserService currentUser, ILogger<CreatePaymentOrderHandler> logger)
    {
        _paymentService = paymentService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(CreatePaymentOrderCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        try
        {
            var approvalUrl = await _paymentService.CreateOrderAsync(userId, request.PlanId, ct);
            return Result<string>.Success(approvalUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create payment order for user {UserId}.", userId);
            return Result<string>.Failure("Payment gateway error. Please try again later.", 500);
        }
    }
}

// ─── Capture PayPal Order ─────────────────────────────────────────
public record CapturePaymentOrderCommand(string OrderId) : IRequest<Result<string>>;

public class CapturePaymentOrderHandler : IRequestHandler<CapturePaymentOrderCommand, Result<string>>
{
    private readonly IPaymentService _paymentService;
    private readonly ICurrentUserService _currentUser;

    public CapturePaymentOrderHandler(IPaymentService paymentService, ICurrentUserService currentUser)
    {
        _paymentService = paymentService;
        _currentUser = currentUser;
    }

    public async Task<Result<string>> Handle(CapturePaymentOrderCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var success = await _paymentService.CaptureOrderAsync(request.OrderId, userId, ct);

        return success
            ? Result<string>.Success("Payment successful. Account upgraded.")
            : Result<string>.Failure("Failed to capture payment or order already processed.", 400);
    }
}
