using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Admin;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Admin.Plans.Queries;

public record GetPlanDefinitionByIdQuery(Guid Id) : IRequest<Result<PlanDefinitionDto>>;

public class GetPlanDefinitionByIdHandler : IRequestHandler<GetPlanDefinitionByIdQuery, Result<PlanDefinitionDto>>
{
    private readonly IUnitOfWork _uow;

    public GetPlanDefinitionByIdHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<PlanDefinitionDto>> Handle(GetPlanDefinitionByIdQuery request, CancellationToken ct)
    {
        var plan = await _uow.PlanDefinitions.GetByIdWithFeaturesAsync(request.Id, ct);
        if (plan == null)
            return Result<PlanDefinitionDto>.NotFound($"Plan with ID '{request.Id}' not found.");

        return Result<PlanDefinitionDto>.Success(new PlanDefinitionDto(
            plan.Id,
            plan.Name,
            plan.NameKey,
            plan.Price,
            plan.Currency,
            plan.Description,
            plan.DurationDays,
            plan.IsRecommended,
            plan.PlanFeatures.Select(pf => new PlanFeatureDto(
                pf.FeatureDefinitionId,
                pf.Feature.Code,
                pf.Feature.Name,
                pf.Value)).ToList(),
            plan.CreatedAt,
            plan.UpdatedAt));
    }
}
