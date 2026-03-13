using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Features.Commands;

public record UpdateFeatureDefinitionCommand(
    Guid Id,
    string Code,
    string Name,
    string Description) : IRequest<Result<FeatureDefinitionDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(FeatureDefinition);
    public string EntityId => Id.ToString();
}

public class UpdateFeatureDefinitionValidator : AbstractValidator<UpdateFeatureDefinitionCommand>
{
    public UpdateFeatureDefinitionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
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
        var existing = await _uow.FeatureDefinitions.GetByIdAsync(request.Id, ct);
        if (existing == null)
            return Result<FeatureDefinitionDto>.NotFound($"Feature with ID '{request.Id}' not found.");

        // Check if code is being changed and if it already exists
        if (existing.Code != request.Code)
        {
            var codeExists = await _uow.FeatureDefinitions.GetByCodeAsync(request.Code, ct);
            if (codeExists != null)
                return Result<FeatureDefinitionDto>.Conflict($"Feature with code '{request.Code}' already exists.");
        }

        existing.Code = request.Code;
        existing.Name = request.Name;
        existing.Description = request.Description;

        _uow.FeatureDefinitions.Update(existing);
        await _uow.SaveChangesAsync(ct);

        return Result<FeatureDefinitionDto>.Success(new FeatureDefinitionDto(
            existing.Id,
            existing.Code,
            existing.Name,
            existing.Description,
            existing.CreatedAt,
            existing.UpdatedAt));
    }
}
