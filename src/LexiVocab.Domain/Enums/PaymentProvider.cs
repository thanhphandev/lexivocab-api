namespace LexiVocab.Domain.Enums;

/// <summary>
/// Supported payment providers. Designed for easy extension.
/// </summary>
public enum PaymentProvider
{
    Mock = 0,
    PayPal = 1,
    Stripe = 2,
    Seapay = 3
}
