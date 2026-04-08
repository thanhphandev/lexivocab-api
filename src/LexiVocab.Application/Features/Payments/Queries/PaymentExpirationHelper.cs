using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Application.Features.Payments.Queries;

/// <summary>
/// Shared helper for payment transaction expiration logic.
/// </summary>
public static class PaymentExpirationHelper
{
    /// <summary>
    /// Checks if a pending transaction has expired and updates it to Expired status.
    /// Returns true if the transaction was expired and updated.
    /// </summary>
    public static bool ExpireIfNeeded(PaymentTransaction tx, DateTime now, Action<string, object[]>? logCallback = null)
    {
        if (tx.Status != PaymentStatus.Pending)
            return false;

        var isExpired = tx.ExpiresAt.HasValue && tx.ExpiresAt.Value <= now;

        if (!isExpired)
            return false;

        tx.Status = PaymentStatus.Expired;
        tx.CancelledAt = now;
        tx.CancelReason = "Expired by configured pending payment expiry.";

        if (tx.Subscription != null && tx.Subscription.Status == SubscriptionStatus.Pending)
        {
            tx.Subscription.Status = SubscriptionStatus.Cancelled;
            logCallback?.Invoke("Transaction {TransactionId} and associated subscription {SubscriptionId} expired.", new object[] { tx.Id, tx.Subscription.Id });
        }
        else
        {
            logCallback?.Invoke("Transaction {TransactionId} expired (no pending subscription found).", new object[] { tx.Id });
        }

        return true;
    }
}
