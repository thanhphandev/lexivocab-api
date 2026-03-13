namespace LexiVocab.Application.DTOs.Admin;

public record PlanDefinitionDto(
    Guid Id,
    string Name,
    string NameKey,
    decimal Price,
    string Currency,
    string Description,
    int DurationDays,
    bool IsRecommended,
    List<PlanFeatureDto> Features,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record PlanFeatureDto(
    Guid FeatureId,
    string FeatureCode,
    string FeatureName,
    string Value);
