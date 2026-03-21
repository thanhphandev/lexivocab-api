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
    DateTime? PaidAt,
    DateTime? ExpiresAt = null,
    DateTime? CancelledAt = null,
    string? ApprovalUrl = null,
    decimal? OriginalAmount = null,
    decimal? DiscountAmount = null,
    string? CouponCode = null,
    string? PlanName = null);

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

public record PlanPricingDto(
    string Id,
    string BillingCycle,
    string Price,
    string Currency,
    int? DurationDays,
    string LabelKey);

public record SubscriptionPlanDto(
    string Id,
    string NameKey,
    string DescriptionKey,
    bool IsRecommended,
    int DisplayOrder,
    List<PlanFeatureDto> Features,
    List<PlanPricingDto> Pricings);

// ─── Requests ────────────────────────────────────────────────
public record CreateOrderRequest(string PricingId, LexiVocab.Domain.Enums.PaymentProvider Provider = LexiVocab.Domain.Enums.PaymentProvider.PayPal, string? CouponCode = null);
public record CaptureOrderRequest(string OrderId);
