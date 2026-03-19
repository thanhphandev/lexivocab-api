using LexiVocab.Application.DTOs.Payment;

namespace LexiVocab.Application.DTOs.Admin;

/// <summary>
/// Request body for updating a feature definition.
/// Matches UI UpdateFeatureDefinitionRequest structure.
/// </summary>
public record UpdateFeatureDefinitionRequest(
    string Description,
    string ValueType,
    string DefaultValue
);

/// <summary>
/// Request body for updating a plan definition.
/// Matches UI UpdatePlanDefinitionRequest structure.
/// </summary>
public record UpdatePlanDefinitionRequest(
    string Name,
    bool IsActive,
    Dictionary<string, string> Features,
    List<PlanPricingDto> Pricings
);
