using LexiVocab.Domain.Common;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// Defines a specific feature or limit that can be controlled per plan.
/// </summary>
public class FeatureDefinition : BaseEntity
{
    public string Code { get; set; } = string.Empty; // e.g., "MAX_WORDS", "AI_ACCESS"
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<PlanFeature> PlanFeatures { get; set; } = [];
}
