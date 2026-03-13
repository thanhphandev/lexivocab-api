using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
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

        var dtos = plans.Select(p => new PlanDefinitionDto(
            p.Id,
            p.Name,
            p.NameKey,
            p.Price,
            p.Currency,
            p.Description,
            p.DurationDays,
            p.IsRecommended,
            p.PlanFeatures.Select(pf => new PlanFeatureDto(
                pf.FeatureDefinitionId,
                pf.Feature.Code,
                pf.Feature.Name,
                pf.Value)).ToList(),
            p.CreatedAt,
            p.UpdatedAt)).ToList();

        return Result<List<PlanDefinitionDto>>.Success(dtos);
    }
}
