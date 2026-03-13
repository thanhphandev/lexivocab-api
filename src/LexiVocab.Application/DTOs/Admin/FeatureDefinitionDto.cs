namespace LexiVocab.Application.DTOs.Admin;

public record FeatureDefinitionDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
