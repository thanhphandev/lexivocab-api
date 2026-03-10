using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Features.Payments.Commands;

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
