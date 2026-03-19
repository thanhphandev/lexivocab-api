using LexiVocab.Domain.Enums;

namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// Abstraction for payment gateways (e.g., PayPal, Stripe, Sepay).
/// Allows easily swapping providers without changing business logic.
/// </summary>
public interface IPaymentService
{
    // Returns the provider type (e.g., PayPal)
    PaymentProvider Provider { get; }

    /// <summary>
    /// Creates a checkout order/session.
    /// Returns the approval URL that the frontend should redirect the user to.
    /// </summary>
    Task<string> CreateOrderAsync(Guid userId, string pricingId, string? couponCode = null, CancellationToken ct = default);

    /// <summary>
    /// Captures the payment after the user approves it on the provider's checkout page.
    /// Returns true if successful and user role has been upgraded.
    /// </summary>
    Task<bool> CaptureOrderAsync(string orderId, Guid userId, CancellationToken ct);

    /// <summary>
    /// Verifies the authenticity of an incoming webhook from the payment provider.
    /// </summary>
    Task<bool> VerifyWebhookSignatureAsync(string body, IDictionary<string, string> headers);

    /// <summary>
    /// Processes a verified webhook event.
    /// Should be called only after signature verification succeeds.
    /// </summary>
    Task ProcessWebhookEventAsync(string body, CancellationToken ct);

    /// <summary>
    /// Gets a static or reconstructible approval URL for a transaction.
    /// Returns null if the provider doesn't support easy resumption (like PayPal).
    /// </summary>
    string? GetApprovalUrl(string reference, decimal amount);

    /// <summary>
    /// Issues a refund for a specific transaction.
    /// Returns true if successful.
    /// </summary>
    Task<Common.Result<bool>> RefundPaymentAsync(string externalTransactionId, string? reason = null, CancellationToken ct = default);
}
