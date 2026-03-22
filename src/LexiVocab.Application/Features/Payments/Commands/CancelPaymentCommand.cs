using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Features.Payments.Commands;

/// <summary>
/// Cancel a pending payment transaction (user-initiated).
/// Only Pending transactions can be cancelled.
/// </summary>
public record CancelPaymentCommand(string Reference) : IRequest<Result>;

public class CancelPaymentHandler : IRequestHandler<CancelPaymentCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailQueueService _emailQueue;
    private readonly IEmailTemplateService _templateService;
    private readonly ILogger<CancelPaymentHandler> _logger;

    public CancelPaymentHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IEmailQueueService emailQueue,
        IEmailTemplateService templateService,
        ILogger<CancelPaymentHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _emailQueue = emailQueue;
        _templateService = templateService;
        _logger = logger;
    }

    public async Task<Result> Handle(CancelPaymentCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;

        var tx = await _uow.PaymentTransactions
            .GetByExternalOrderIdWithDetailsAsync(request.Reference, ct);

        if (tx == null)
            return Result.NotFound("Payment transaction not found.", ErrorCode.PAYMENT_ORDER_NOT_FOUND);

        // Verify ownership
        if (tx.UserId != userId)
            return Result.Failure("You do not have permission to cancel this payment.", 403, ErrorCode.AUTHZ_RESOURCE_FORBIDDEN);

        // Can only cancel pending transactions
        if (tx.Status != PaymentStatus.Pending)
            return Result.Failure($"Cannot cancel a payment with status '{tx.Status}'. Only pending payments can be cancelled.", 400, ErrorCode.PAYMENT_ALREADY_PROCESSED);

        // Update transaction status
        tx.Status = PaymentStatus.Cancelled;
        tx.CancelledAt = DateTime.UtcNow;
        tx.CancelReason = "Cancelled by user.";

        // Cancel the associated subscription if it's still pending
        if (tx.Subscription != null && tx.Subscription.Status == SubscriptionStatus.Pending)
            tx.Subscription.Status = SubscriptionStatus.Cancelled;

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Payment {Reference} cancelled by user {UserId}", request.Reference, userId);

        // Send cancellation email
        try
        {
            var user = tx.User;
            var html = await _templateService.RenderTemplateAsync("PaymentCancelled", new Dictionary<string, string>
            {
                { "FullName", user.FullName },
                { "Amount", $"${tx.Amount:F2} {tx.Currency}" },
                { "Reference", tx.ExternalOrderId }
            });
            _emailQueue.EnqueueEmail(user.Email, "🚫 Payment Cancelled", html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment cancellation email for {Reference}", request.Reference);
        }

        return Result.Success();
    }
}
