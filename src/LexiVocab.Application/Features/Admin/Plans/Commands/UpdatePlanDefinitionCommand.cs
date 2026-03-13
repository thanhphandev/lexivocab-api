using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Plans.Commands;

public record UpdatePlanFeatureRequest(Guid FeatureId, string Value);

public record UpdatePlanDefinitionCommand(
    Guid Id,
    string Name,
    string NameKey,
    decimal Price,
    string Currency,
    string Description,
    int DurationDays,
    bool IsRecommended,
    List<UpdatePlanFeatureRequest> Features) : IRequest<Result<PlanDefinitionDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(PlanDefinition);
    public string EntityId => Id.ToString();
}

public class UpdatePlanDefinitionValidator : AbstractValidator<UpdatePlanDefinitionCommand>
{
    public UpdatePlanDefinitionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DurationDays).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.Features).SetValidator(new UpdatePlanFeatureRequestValidator());
    }
}

public class UpdatePlanFeatureRequestValidator : AbstractValidator<UpdatePlanFeatureRequest>
{
    public UpdatePlanFeatureRequestValidator()
    {
        RuleFor(x => x.FeatureId).NotEmpty();
        RuleFor(x => x.Value).NotEmpty().MaximumLength(100);
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
        plan.NameKey = request.NameKey;
        plan.Price = request.Price;
        plan.Currency = request.Currency;
        plan.Description = request.Description;
        plan.DurationDays = request.DurationDays;
        plan.IsRecommended = request.IsRecommended;

        // Rebuild PlanFeatures
        plan.PlanFeatures.Clear();
        foreach (var reqFeature in request.Features)
        {
            plan.PlanFeatures.Add(new PlanFeature
            {
                PlanDefinitionId = plan.Id,
                FeatureDefinitionId = reqFeature.FeatureId,
                Value = reqFeature.Value
            });
        }

        _uow.PlanDefinitions.Update(plan);
        await _uow.SaveChangesAsync(ct);

        var updatedPlan = await _uow.PlanDefinitions.GetByIdWithFeaturesAsync(plan.Id, ct);

        return Result<PlanDefinitionDto>.Success(new PlanDefinitionDto(
            updatedPlan!.Id,
            updatedPlan.Name,
            updatedPlan.NameKey,
            updatedPlan.Price,
            updatedPlan.Currency,
            updatedPlan.Description,
            updatedPlan.DurationDays,
            updatedPlan.IsRecommended,
            updatedPlan.PlanFeatures.Select(pf => new PlanFeatureDto(
                pf.FeatureDefinitionId,
                pf.Feature.Code,
                pf.Feature.Name,
                pf.Value)).ToList(),
            updatedPlan.CreatedAt,
            updatedPlan.UpdatedAt));
    }
}
