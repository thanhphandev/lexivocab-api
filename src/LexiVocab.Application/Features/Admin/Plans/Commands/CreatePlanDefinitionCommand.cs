using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Plans.Commands;

public record CreatePlanFeatureRequest(Guid FeatureId, string Value);

public record CreatePlanDefinitionCommand(
    string Name,
    string NameKey,
    decimal Price,
    string Currency,
    string Description,
    int DurationDays,
    bool IsRecommended,
    List<CreatePlanFeatureRequest> Features) : IRequest<Result<PlanDefinitionDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(PlanDefinition);
}

public class CreatePlanDefinitionValidator : AbstractValidator<CreatePlanDefinitionCommand>
{
    public CreatePlanDefinitionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DurationDays).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.Features).SetValidator(new CreatePlanFeatureRequestValidator());
    }
}

public class CreatePlanFeatureRequestValidator : AbstractValidator<CreatePlanFeatureRequest>
{
    public CreatePlanFeatureRequestValidator()
    {
        RuleFor(x => x.FeatureId).NotEmpty();
        RuleFor(x => x.Value).NotEmpty().MaximumLength(100);
    }
}

public class CreatePlanDefinitionHandler : IRequestHandler<CreatePlanDefinitionCommand, Result<PlanDefinitionDto>>
{
    private readonly IUnitOfWork _uow;

    public CreatePlanDefinitionHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<PlanDefinitionDto>> Handle(CreatePlanDefinitionCommand request, CancellationToken ct)
    {
        var existing = await _uow.PlanDefinitions.GetByNameAsync(request.Name, ct);
        if (existing != null)
            return Result<PlanDefinitionDto>.Conflict($"Plan with name '{request.Name}' already exists.");

        var plan = new PlanDefinition
        {
            Name = request.Name,
            NameKey = request.NameKey,
            Price = request.Price,
            Currency = request.Currency,
            Description = request.Description,
            DurationDays = request.DurationDays,
            IsRecommended = request.IsRecommended,
            PlanFeatures = request.Features.Select(f => new PlanFeature
            {
                FeatureDefinitionId = f.FeatureId,
                Value = f.Value
            }).ToList()
        };

        _uow.PlanDefinitions.Add(plan);
        await _uow.SaveChangesAsync(ct);

        // Fetch back with included features
        var savedPlan = await _uow.PlanDefinitions.GetByIdWithFeaturesAsync(plan.Id, ct);

        return Result<PlanDefinitionDto>.Created(new PlanDefinitionDto(
            savedPlan!.Id,
            savedPlan.Name,
            savedPlan.NameKey,
            savedPlan.Price,
            savedPlan.Currency,
            savedPlan.Description,
            savedPlan.DurationDays,
            savedPlan.IsRecommended,
            savedPlan.PlanFeatures.Select(pf => new PlanFeatureDto(
                pf.FeatureDefinitionId,
                pf.Feature.Code,
                pf.Feature.Name,
                pf.Value)).ToList(),
            savedPlan.CreatedAt,
            savedPlan.UpdatedAt));
    }
}
