using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Application.Features.Payments.Queries;

/// <summary>
/// Shared helper for payment transaction expiration logic.
/// </summary>
internal static class PaymentExpirationHelper
{
    /// <summary>
    /// Checks if a pending transaction has expired and updates it to Expired status.
    /// Returns true if the transaction was expired and updated.
    /// </summary>
    public static bool ExpireIfNeeded(PaymentTransaction tx, DateTime now)
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
            tx.Subscription.Status = SubscriptionStatus.Cancelled;

        return true;
    }
}
