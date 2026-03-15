using LexiVocab.Domain.Common;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// Stores the core metadata for a subscription plan tier.
/// IntervalType: Month, Year, Lifetime - for UI display
/// IsActive: Controls visibility for new subscriptions
/// DurationDays: Actual billing duration in days (0 = lifetime)
/// </summary>
public class PlanDefinition : BaseEntity
{
    public string Name { get; set; } = string.Empty; // e.g., "Free", "Premium", "Business"
    public string NameKey { get; set; } = string.Empty; // For localization, e.g., "free_plan"
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public string Description { get; set; } = string.Empty;
    public string IntervalType { get; set; } = "Month"; // Month, Year, Lifetime
    public int DurationDays { get; set; } // 0 for lifetime, calculated from IntervalType
    public bool IsActive { get; set; } = true; // Visible for new signups
    public bool IsRecommended { get; set; } // Show as recommended plan

    // Navigation properties
    public ICollection<PlanFeature> PlanFeatures { get; set; } = [];
    public ICollection<Subscription> Subscriptions { get; set; } = [];
}
