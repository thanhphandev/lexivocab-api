using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Features.Payments.Commands;

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
