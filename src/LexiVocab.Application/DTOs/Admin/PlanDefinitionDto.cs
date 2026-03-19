using LexiVocab.Application.DTOs.Payment;

namespace LexiVocab.Application.DTOs.Admin;

/// <summary>
/// Plan definition DTO matching UI expectations.
/// Uses Dictionary for features to match frontend JSON object structure.
/// Includes List of PlanPricings for UI management.
/// </summary>
public record PlanDefinitionDto(
    Guid Id,
    string Name,
    bool IsActive,
    Dictionary<string, string> Features,  // Key: FeatureCode, Value: FeatureValue
    List<PlanPricingDto> Pricings,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Internal DTO for plan-feature relationship (used in queries/handlers).
/// </summary>
public record PlanFeatureDto(
    Guid FeatureId,
    string FeatureCode,
    string FeatureName,
    string Value);
