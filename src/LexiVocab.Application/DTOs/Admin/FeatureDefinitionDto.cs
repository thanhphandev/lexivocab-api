namespace LexiVocab.Application.DTOs.Admin;

/// <summary>
/// Feature definition DTO matching UI expectations.
/// </summary>
public record FeatureDefinitionDto(
    Guid Id,
    string Code,
    string Description,
    string ValueType,      // boolean, integer, string
    string DefaultValue,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
