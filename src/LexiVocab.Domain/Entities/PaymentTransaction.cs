using LexiVocab.Domain.Common;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// Records individual payment transactions for audit and idempotency.
/// The ExternalOrderId column has a unique index to prevent duplicate processing
/// from webhook retries or race conditions.
/// </summary>
public class PaymentTransaction : BaseEntity
{
    // ─── Foreign Keys ────────────────────────────────────────────
    public Guid SubscriptionId { get; set; }
    public Guid UserId { get; set; }

    // ─── Payment Details ─────────────────────────────────────────
    /// <summary>Which payment gateway processed this transaction.</summary>
    public PaymentProvider Provider { get; set; }

    /// <summary>
    /// External order/transaction ID from provider (e.g., PayPal Order ID).
    /// UNIQUE — serves as the idempotency key.
    /// </summary>
    public string ExternalOrderId { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>When the payment was actually confirmed/captured.</summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>Raw webhook/response payload for audit trail.</summary>
    public string? RawPayload { get; set; }

    /// <summary>Unique ID from the provider (e.g. SePay ID or PayPal Capture ID).</summary>
    public string? ProviderResponseId { get; set; }

    // ─── Navigation ──────────────────────────────────────────────
    public Subscription Subscription { get; set; } = null!;
    public User User { get; set; } = null!;
}
