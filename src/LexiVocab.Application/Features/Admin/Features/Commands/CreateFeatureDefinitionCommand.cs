using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Features.Commands;

/// <summary>
/// Command to create a new feature definition.
/// Matches UI CreateFeatureDefinitionRequest structure.
/// </summary>
public record CreateFeatureDefinitionCommand(
    string Code,
    string Description,
    string ValueType,
    string DefaultValue) : IRequest<Result<FeatureDefinitionDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(FeatureDefinition);
}

public class CreateFeatureDefinitionValidator : AbstractValidator<CreateFeatureDefinitionCommand>
{
    private static readonly string[] ValidValueTypes = ["boolean", "integer", "string"];

    public CreateFeatureDefinitionValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.ValueType)
            .NotEmpty()
            .Must(vt => ValidValueTypes.Contains(vt))
            .WithMessage($"ValueType must be one of: {string.Join(", ", ValidValueTypes)}");
        RuleFor(x => x.DefaultValue).MaximumLength(100);
    }
}

public class CreateFeatureDefinitionHandler : IRequestHandler<CreateFeatureDefinitionCommand, Result<FeatureDefinitionDto>>
{
    private readonly IUnitOfWork _uow;

    public CreateFeatureDefinitionHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<FeatureDefinitionDto>> Handle(CreateFeatureDefinitionCommand request, CancellationToken ct)
    {
        var existing = await _uow.FeatureDefinitions.GetByCodeAsync(request.Code, ct);
        if (existing != null)
            return Result<FeatureDefinitionDto>.Conflict($"Feature with code '{request.Code}' already exists.");

        var feature = new FeatureDefinition
        {
            Code = request.Code,
            Name = request.Code, // Use code as name for now
            Description = request.Description,
            ValueType = request.ValueType,
            DefaultValue = request.DefaultValue
        };

        _uow.FeatureDefinitions.Add(feature);
        await _uow.SaveChangesAsync(ct);

        return Result<FeatureDefinitionDto>.Created(new FeatureDefinitionDto(
            feature.Id,
            feature.Code,
            feature.Description,
            feature.ValueType,
            feature.DefaultValue,
            feature.CreatedAt,
            feature.UpdatedAt));
    }
}
