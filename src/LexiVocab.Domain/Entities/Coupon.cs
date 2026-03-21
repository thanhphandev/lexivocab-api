using LexiVocab.Domain.Common;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// Represents a discount code that can be applied to plan purchases.
/// </summary>
public class Coupon : BaseEntity
{
    /// <summary>
    /// The unique code string the user enters (e.g. "SUMMER50", "WELCOME10").
    /// Stored in uppercase.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    public DiscountType DiscountType { get; set; }

    /// <summary>
    /// The value of the discount. If Percentage, 50 = 50%. If FixedAmount, 50 = $50.
    /// </summary>
    public decimal DiscountValue { get; set; }

    /// <summary>
    /// Optional currency code (e.g., "USD", "VND") for FixedAmount coupons.
    /// Ignored if DiscountType is Percentage.
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Optional date when the coupon becomes valid.
    /// </summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>
    /// Optional date when the coupon expires.
    /// </summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>
    /// Maximum number of times this coupon can be used across all users.
    /// </summary>
    public int? MaxUses { get; set; }

    /// <summary>
    /// Current number of times this coupon has been used.
    /// </summary>
    public int CurrentUses { get; set; }

    /// <summary>
    /// Whether the coupon is currently active. Admin can toggle this to manually disable a coupon.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// A description for internal admin use.
    /// </summary>
    public string? Description { get; set; }

    // ─── Navigation ──────────────────────────────────────────────
    
    /// <summary>
    /// Transactions that applied this coupon.
    /// </summary>
    public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();

    // ─── Domain Logic ────────────────────────────────────────────

    public bool IsValid()
    {
        if (!IsActive) return false;
        if (MaxUses.HasValue && CurrentUses >= MaxUses.Value) return false;

        var now = DateTime.UtcNow;
        if (ValidFrom.HasValue && now < ValidFrom.Value) return false;
        if (ValidUntil.HasValue && now > ValidUntil.Value) return false;

        return true;
    }
}
