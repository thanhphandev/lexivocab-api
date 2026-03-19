using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Transactions.Commands;

public record ProcessRefundCommand(Guid TransactionId, string Reason) : IRequest<Result<bool>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(Domain.Entities.PaymentTransaction);
    public string EntityId => TransactionId.ToString();
}

public class ProcessRefundHandler : IRequestHandler<ProcessRefundCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly IPaymentServiceFactory _paymentFactory;

    public ProcessRefundHandler(IUnitOfWork uow, IPaymentServiceFactory paymentFactory)
    {
        _uow = uow;
        _paymentFactory = paymentFactory;
    }

    public async Task<Result<bool>> Handle(ProcessRefundCommand request, CancellationToken ct)
    {
        var transaction = await _uow.PaymentTransactions.GetByIdAsync(request.TransactionId, ct);
        
        if (transaction == null)
            return Result<bool>.NotFound($"Transaction with ID '{request.TransactionId}' not found.");

        if (transaction.Status != PaymentStatus.Completed)
            return Result<bool>.Failure($"Cannot refund a transaction with status {transaction.Status}. Only Completed transactions can be refunded.");

        if (string.IsNullOrEmpty(transaction.ExternalOrderId))
            return Result<bool>.Failure("This transaction does not have an external order ID and cannot be processed for refund automatically.");

        // Load Subscription
        var subscription = await _uow.Subscriptions.GetByIdAsync(transaction.SubscriptionId, ct);
        if (subscription == null)
            return Result<bool>.Failure("Associated subscription not found.");

        try
        {
            var paymentService = _paymentFactory.GetService(transaction.Provider);
            var refundResult = await paymentService.RefundPaymentAsync(transaction.ExternalOrderId, request.Reason, ct);

            if (!refundResult.IsSuccess)
            {
                return Result<bool>.Failure($"Payment provider refund failed: {refundResult.Error}");
            }

            // Update local state
            transaction.Status = PaymentStatus.Refunded;
            transaction.CancelReason = request.Reason;
            
            subscription.Status = SubscriptionStatus.Cancelled;

            _uow.PaymentTransactions.Update(transaction);
            _uow.Subscriptions.Update(subscription);
            
            await _uow.SaveChangesAsync(ct);

            return Result<bool>.Success(true);
        }
        catch (NotSupportedException ex)
        {
            return Result<bool>.Failure($"Provider {transaction.Provider} does not support automatic refunds: {ex.Message}. Please refund manually, then update status.");
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"An error occurred while processing refund: {ex.Message}");
        }
    }
}
