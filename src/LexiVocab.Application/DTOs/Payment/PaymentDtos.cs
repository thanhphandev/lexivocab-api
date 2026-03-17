namespace LexiVocab.Application.DTOs.Payment;

/// <summary>DTO for current subscription information.</summary>
public record SubscriptionDto(
    Guid Id,
    string Plan,
    string Status,
    DateTime StartDate,
    DateTime? EndDate,
    string Provider,
    string? ExternalSubscriptionId,
    int? DurationMonths);

/// <summary>DTO for individual payment transaction history.</summary>
public record PaymentHistoryDto(
    Guid Id,
    string Provider,
    string ExternalOrderId,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? ExpiresAt = null,
    DateTime? CancelledAt = null,
    string? ApprovalUrl = null);

/// <summary>DTO for billing overview page.</summary>
public record BillingOverviewDto(
    SubscriptionDto? ActiveSubscription,
    Dictionary<string, string> FeatureFlags,
    string Plan,
    DateTime? PlanExpiresAt,
    int TotalTransactions);

public record PaymentStatusDto(
    string Status,
    DateTime? ExpiresAt);

public record PlanFeatureDto(
    string TextKey,
    bool Included,
    Dictionary<string, object>? Params = null);

public record SubscriptionPlanDto(
    string Id,
    string NameKey,
    string Price,
    string IntervalKey,
    string DescriptionKey,
    bool IsRecommended,
    List<PlanFeatureDto> Features);

// ─── Requests ────────────────────────────────────────────────
public record CreateOrderRequest(string PlanId, LexiVocab.Domain.Enums.PaymentProvider Provider = LexiVocab.Domain.Enums.PaymentProvider.PayPal, int DurationMonths = 1);
public record CaptureOrderRequest(string OrderId);
