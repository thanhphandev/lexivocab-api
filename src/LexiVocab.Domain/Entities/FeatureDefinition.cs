using LexiVocab.Domain.Common;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// Defines a specific feature or limit that can be controlled per plan.
/// ValueType defines the data type: boolean, integer, string
/// DefaultValue is the fallback value when not specified in a plan
/// </summary>
public class FeatureDefinition : BaseEntity
{
    public string Code { get; set; } = string.Empty; // e.g., "MAX_WORDS", "AI_ACCESS"
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ValueType { get; set; } = "boolean"; // boolean, integer, string
    public string DefaultValue { get; set; } = "false";

    // Navigation properties
    public ICollection<PlanFeature> PlanFeatures { get; set; } = [];
}
