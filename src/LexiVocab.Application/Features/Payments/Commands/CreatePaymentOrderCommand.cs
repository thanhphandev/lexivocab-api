using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Features.Payments.Commands;

public record CreatePaymentOrderCommand(string PricingId, PaymentProvider Provider, string? CouponCode = null) : IRequest<Result<string>>;

public class CreatePaymentOrderHandler : IRequestHandler<CreatePaymentOrderCommand, Result<string>>
{
    private readonly IPaymentServiceFactory _paymentFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<CreatePaymentOrderHandler> _logger;

    public CreatePaymentOrderHandler(IPaymentServiceFactory paymentFactory, ICurrentUserService currentUser, IUnitOfWork uow, ILogger<CreatePaymentOrderHandler> logger)
    {
        _paymentFactory = paymentFactory;
        _currentUser = currentUser;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(CreatePaymentOrderCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var activeSub = await _uow.Subscriptions.GetActiveByUserIdAsync(userId, ct);
        if (activeSub != null)
            return Result<string>.Conflict("You already have an active subscription.", ErrorCode.SUB_ALREADY_ACTIVE);

        if (!Guid.TryParse(request.PricingId, out var pricingGuid))
            return Result<string>.NotFound("Invalid subscription pricing ID.", ErrorCode.SUB_PLAN_NOT_FOUND);

        var pricing = await _uow.PlanPricings.GetByIdAsync(pricingGuid, ct);
        if (pricing == null)
            return Result<string>.NotFound("Subscription plan pricing not found.", ErrorCode.SUB_PLAN_NOT_FOUND);

        try
        {
            var paymentService = _paymentFactory.GetService(request.Provider);
            var approvalUrl = await paymentService.CreateOrderAsync(userId, request.PricingId, request.CouponCode, ct);
            return Result<string>.Success(approvalUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create payment order for user {UserId} using {Provider}.", userId, request.Provider);
            return Result<string>.Failure("Payment gateway error. Please try again later.", 500, ErrorCode.PAYMENT_PROVIDER_ERROR);
        }
    }
}
