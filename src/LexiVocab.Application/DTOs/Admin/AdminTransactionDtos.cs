namespace LexiVocab.Application.DTOs.Admin;

public record AdminTransactionDto(
    Guid Id,
    string ExternalOrderId,
    decimal Amount,
    string Currency,
    string Status,
    string Provider,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? CancelledAt,
    string? CancelReason,
    Guid UserId,
    string UserEmail,
    Guid SubscriptionId,
    string PlanName,
    Guid? CouponId,
    string? CouponCode
);

public record RefundTransactionRequest(string Reason);
