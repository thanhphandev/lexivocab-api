namespace LexiVocab.Domain.Enums;

/// <summary>
/// Type of discount applied by a coupon.
/// </summary>
public enum DiscountType
{
    /// <summary>
    /// Percentage discount (e.g., 20%).
    /// </summary>
    Percentage,

    /// <summary>
    /// Fixed amount discount (e.g., $10 off).
    /// </summary>
    FixedAmount
}
