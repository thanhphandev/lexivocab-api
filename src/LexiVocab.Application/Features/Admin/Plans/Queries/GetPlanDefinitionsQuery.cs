using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Application.DTOs.Payment;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Plans.Queries;

public record GetPlanDefinitionsQuery() : IRequest<Result<List<PlanDefinitionDto>>>;

public class GetPlanDefinitionsHandler : IRequestHandler<GetPlanDefinitionsQuery, Result<List<PlanDefinitionDto>>>
{
    private readonly IUnitOfWork _uow;

    public GetPlanDefinitionsHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<List<PlanDefinitionDto>>> Handle(GetPlanDefinitionsQuery request, CancellationToken ct)
    {
        var plans = await _uow.PlanDefinitions.GetAllWithFeaturesAsync(ct);

        var dtos = plans.Select(plan => new PlanDefinitionDto(
            plan.Id,
            plan.Name,
            plan.IsActive,
            plan.PlanFeatures.ToDictionary(pf => pf.Feature.Code, pf => pf.Value),
            plan.Pricings.Select(pr => new PlanPricingDto(
                pr.Id.ToString(),
                pr.BillingCycle.ToString(),
                pr.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                pr.Currency,
                pr.DurationDays,
                pr.LabelKey)).ToList(),
            plan.CreatedAt,
            plan.UpdatedAt)).ToList();

        return Result<List<PlanDefinitionDto>>.Success(dtos);
    }
}
