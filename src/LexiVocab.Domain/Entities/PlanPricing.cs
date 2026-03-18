using LexiVocab.Domain.Common;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// Maps a PlanDefinition (Product features) to a specific Price and Billing Cycle.
/// Supports multiple pricing tiers (e.g., Monthly, Yearly) for a single plan.
/// </summary>
public class PlanPricing : BaseEntity
{
    // ─── Foreign Keys ────────────────────────────────────────────
    public Guid PlanDefinitionId { get; set; }
    public PlanDefinition Plan { get; set; } = null!;

    // ─── Pricing Details ─────────────────────────────────────────
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";

    /// <summary>
    /// For fixed-term access (e.g., 30 days, 365 days). Null means Lifetime.
    /// This overrides the Enum value for actual date calculation to allow precise controls (e.g., 28 days vs 31 days).
    /// </summary>
    public int? DurationDays { get; set; }

    // ─── Presentation ────────────────────────────────────────────
    /// <summary>Localization key for the duration label, e.g., "duration_1m"</summary>
    public string LabelKey { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // ─── Navigation ──────────────────────────────────────────────
    public ICollection<Subscription> Subscriptions { get; set; } = [];
}
