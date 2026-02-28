namespace LexiVocab.Domain.Enums;

/// <summary>
/// Payment transaction states. Covers the full lifecycle of a payment.
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3
}
