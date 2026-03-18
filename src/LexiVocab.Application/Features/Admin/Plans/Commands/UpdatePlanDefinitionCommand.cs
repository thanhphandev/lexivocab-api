using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Plans.Commands;

/// <summary>
/// Command to update an existing plan definition.
/// Matches UI UpdatePlanDefinitionRequest structure.
/// </summary>
public record UpdatePlanDefinitionCommand(
    Guid Id,
    string Name,
    bool IsActive,
    Dictionary<string, string> Features) : IRequest<Result<PlanDefinitionDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(PlanDefinition);
    public string EntityId => Id.ToString();
}

public class UpdatePlanDefinitionValidator : AbstractValidator<UpdatePlanDefinitionCommand>
{
    private static readonly string[] ValidIntervals = ["Month", "Year", "Lifetime"];

    public UpdatePlanDefinitionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Features).NotNull();
    }
}

public class UpdatePlanDefinitionHandler : IRequestHandler<UpdatePlanDefinitionCommand, Result<PlanDefinitionDto>>
{
    private readonly IUnitOfWork _uow;

    public UpdatePlanDefinitionHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<PlanDefinitionDto>> Handle(UpdatePlanDefinitionCommand request, CancellationToken ct)
    {
        var plan = await _uow.PlanDefinitions.GetByIdWithFeaturesAsync(request.Id, ct);
        if (plan == null)
            return Result<PlanDefinitionDto>.NotFound($"Plan with ID '{request.Id}' not found.");

        if (plan.Name != request.Name)
        {
            var existingName = await _uow.PlanDefinitions.GetByNameAsync(request.Name, ct);
            if (existingName != null)
                return Result<PlanDefinitionDto>.Conflict($"Plan with name '{request.Name}' already exists.");
        }

        plan.Name = request.Name;
        plan.NameKey = $"plan_{request.Name.ToLowerInvariant().Replace(" ", "_")}";
        plan.IsActive = request.IsActive;

        // Rebuild PlanFeatures from Dictionary
        plan.PlanFeatures.Clear();
        foreach (var kvp in request.Features)
        {
            var featureDef = await _uow.FeatureDefinitions.GetByCodeAsync(kvp.Key, ct);
            if (featureDef != null)
            {
                plan.PlanFeatures.Add(new PlanFeature
                {
                    PlanDefinitionId = plan.Id,
                    FeatureDefinitionId = featureDef.Id,
                    Value = kvp.Value
                });
            }
        }

        _uow.PlanDefinitions.Update(plan);
        await _uow.SaveChangesAsync(ct);

        // Build response with Dictionary features
        var responseFeatures = plan.PlanFeatures.ToDictionary(
            pf => pf.Feature?.Code ?? "",
            pf => pf.Value);

        return Result<PlanDefinitionDto>.Success(new PlanDefinitionDto(
            plan.Id,
            plan.Name,
            plan.IsActive,
            responseFeatures,
            plan.CreatedAt,
            plan.UpdatedAt));
    }
}
