using LexiVocab.Domain.Common;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// Tracks a user's subscription to a plan.
/// A user may have multiple historical subscriptions (expired/cancelled),
/// but only one active subscription at any time.
/// </summary>
public class Subscription : BaseEntity
{
    // ─── Foreign Keys ────────────────────────────────────────────
    public Guid UserId { get; set; }

    // ─── Plan Details ────────────────────────────────────────────
    public Guid PlanDefinitionId { get; set; }
    public PlanDefinition PlanDefinition { get; set; } = null!;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    /// <summary>When this subscription period begins.</summary>
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    /// <summary>When this subscription period ends. Null = lifetime.</summary>
    public DateTime? EndDate { get; set; }

    // ─── Payment Provider Reference ──────────────────────────────
    /// <summary>Which provider processed the payment.</summary>
    public PaymentProvider Provider { get; set; } = PaymentProvider.Mock;

    /// <summary>External subscription ID from the payment provider (e.g., PayPal subscription ID).</summary>
    public string? ExternalSubscriptionId { get; set; }

    // ─── Navigation ──────────────────────────────────────────────
    public User User { get; set; } = null!;
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = [];
}
