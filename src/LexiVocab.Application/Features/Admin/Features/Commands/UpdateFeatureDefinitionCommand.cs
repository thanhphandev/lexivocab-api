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
/// Command to update an existing feature definition.
/// Matches UI UpdateFeatureDefinitionRequest structure.
/// </summary>
public record UpdateFeatureDefinitionCommand(
    Guid Id,
    string Description,
    string ValueType,
    string DefaultValue) : IRequest<Result<FeatureDefinitionDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(FeatureDefinition);
    public string EntityId => Id.ToString();
}

public class UpdateFeatureDefinitionValidator : AbstractValidator<UpdateFeatureDefinitionCommand>
{
    private static readonly string[] ValidValueTypes = ["boolean", "integer", "string"];

    public UpdateFeatureDefinitionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.ValueType)
            .NotEmpty()
            .Must(vt => ValidValueTypes.Contains(vt))
            .WithMessage($"ValueType must be one of: {string.Join(", ", ValidValueTypes)}");
        RuleFor(x => x.DefaultValue).MaximumLength(100);
    }
}

public class UpdateFeatureDefinitionHandler : IRequestHandler<UpdateFeatureDefinitionCommand, Result<FeatureDefinitionDto>>
{
    private readonly IUnitOfWork _uow;

    public UpdateFeatureDefinitionHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<FeatureDefinitionDto>> Handle(UpdateFeatureDefinitionCommand request, CancellationToken ct)
    {
        var feature = await _uow.FeatureDefinitions.GetByIdAsync(request.Id, ct);
        if (feature == null)
            return Result<FeatureDefinitionDto>.NotFound($"Feature with ID '{request.Id}' not found.");

        feature.Description = request.Description;
        feature.ValueType = request.ValueType;
        feature.DefaultValue = request.DefaultValue;

        _uow.FeatureDefinitions.Update(feature);
        await _uow.SaveChangesAsync(ct);

        return Result<FeatureDefinitionDto>.Success(new FeatureDefinitionDto(
            feature.Id,
            feature.Code,
            feature.Description,
            feature.ValueType,
            feature.DefaultValue,
            feature.CreatedAt,
            feature.UpdatedAt));
    }
}
