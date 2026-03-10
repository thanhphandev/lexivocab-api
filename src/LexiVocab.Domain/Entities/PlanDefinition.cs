using LexiVocab.Domain.Common;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// Stores the core metadata for a subscription plan tier.
/// </summary>
public class PlanDefinition : BaseEntity
{
    public string Name { get; set; } = string.Empty; // e.g., "Free", "Premium", "Business"
    public string NameKey { get; set; } = string.Empty; // For localization, e.g., "free_plan"
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public string Description { get; set; } = string.Empty;
    public int DurationDays { get; set; } // 0 for lifetime
    public bool IsRecommended { get; set; }

    // Navigation properties
    public ICollection<PlanFeature> PlanFeatures { get; set; } = [];
    public ICollection<Subscription> Subscriptions { get; set; } = [];
}
