using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Application.DTOs.Payment;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Plans.Commands;

/// <summary>
/// Command to create a new plan definition.
/// Matches UI CreatePlanDefinitionRequest structure with Dictionary features.
/// </summary>
public record CreatePlanDefinitionCommand(
    string Name,
    bool IsActive,
    Dictionary<string, string> Features,
    List<PlanPricingDto> Pricings) : IRequest<Result<PlanDefinitionDto>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.SystemSettingUpdated;
    public string EntityType => nameof(PlanDefinition);
}

public class CreatePlanDefinitionValidator : AbstractValidator<CreatePlanDefinitionCommand>
{
    private static readonly string[] ValidIntervals = ["Month", "Year", "Lifetime"];

    public CreatePlanDefinitionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Features).NotNull();
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

        // Convert Dictionary features to PlanFeature entities
        var planFeatures = new List<PlanFeature>();
        foreach (var kvp in request.Features)
        {
            var featureDef = await _uow.FeatureDefinitions.GetByCodeAsync(kvp.Key, ct);
            if (featureDef != null)
            {
                planFeatures.Add(new PlanFeature
                {
                    FeatureDefinitionId = featureDef.Id,
                    Value = kvp.Value
                });
            }
        }

        var pricings = new List<PlanPricing>();
        if (request.Pricings != null)
        {
            foreach (var p in request.Pricings)
            {
                pricings.Add(new PlanPricing
                {
                    BillingCycle = Enum.Parse<BillingCycle>(p.BillingCycle, true),
                    Price = decimal.Parse(p.Price, System.Globalization.CultureInfo.InvariantCulture),
                    Currency = p.Currency,
                    DurationDays = p.DurationDays,
                    LabelKey = p.LabelKey,
                    SortOrder = 0,
                    IsActive = true
                });
            }
        }

        var plan = new PlanDefinition
        {
            Name = request.Name,
            NameKey = $"plan_{request.Name.ToLowerInvariant().Replace(" ", "_")}",
            Description = "", // Can be set later via update
            IsActive = request.IsActive,
            IsRecommended = false,
            PlanFeatures = planFeatures,
            Pricings = pricings
        };

        _uow.PlanDefinitions.Add(plan);
        await _uow.SaveChangesAsync(ct);

        // Build response with Dictionary features
        var responseFeatures = plan.PlanFeatures.ToDictionary(
            pf => pf.Feature?.Code ?? "",
            pf => pf.Value);

        return Result<PlanDefinitionDto>.Created(new PlanDefinitionDto(
            plan.Id,
            plan.Name,
            plan.IsActive,
            responseFeatures,
            plan.Pricings.Select(pr => new LexiVocab.Application.DTOs.Payment.PlanPricingDto(
                pr.Id.ToString(),
                pr.BillingCycle.ToString(),
                pr.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                pr.Currency,
                pr.DurationDays,
                pr.LabelKey)).ToList(),
            plan.CreatedAt,
            plan.UpdatedAt));
    }
}
