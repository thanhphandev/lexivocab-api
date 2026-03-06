namespace LexiVocab.Domain.Enums;

/// <summary>
/// Lifecycle states for a subscription.
/// </summary>
public enum SubscriptionStatus
{
    Active = 0,
    Expired = 1,
    Cancelled = 2,
    Pending = 3
}
