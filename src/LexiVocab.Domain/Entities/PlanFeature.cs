using LexiVocab.Domain.Common;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// Connects a Plan to a specific Feature with a value (limit or Boolean access).
/// </summary>
public class PlanFeature
{
    public Guid PlanDefinitionId { get; set; }
    public PlanDefinition Plan { get; set; } = null!;

    public Guid FeatureDefinitionId { get; set; }
    public FeatureDefinition Feature { get; set; } = null!;

    /// <summary>
    /// The value of the feature for this plan (e.g., "50", "true", "Unlimited").
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
