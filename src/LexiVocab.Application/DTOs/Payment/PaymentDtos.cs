namespace LexiVocab.Application.DTOs.Payment;

/// <summary>DTO for current subscription information.</summary>
public record SubscriptionDto(
    Guid Id,
    string Plan,
    string Status,
    DateTime StartDate,
    DateTime? EndDate,
    string Provider,
    string? ExternalSubscriptionId);

/// <summary>DTO for individual payment transaction history.</summary>
public record PaymentHistoryDto(
    Guid Id,
    string Provider,
    string ExternalOrderId,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAt,
    DateTime? PaidAt);

/// <summary>DTO for billing overview page.</summary>
public record BillingOverviewDto(
    SubscriptionDto? ActiveSubscription,
    bool IsPremium,
    string Plan,
    DateTime? PlanExpiresAt,
    int TotalTransactions);

// ─── Requests ────────────────────────────────────────────────
public record CreateOrderRequest(string PlanId);
public record CaptureOrderRequest(string OrderId);
