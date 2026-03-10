using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Payment;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Payments.Queries;

public record GetSubscriptionPlansQuery() : IRequest<Result<List<SubscriptionPlanDto>>>;

public class GetSubscriptionPlansHandler : IRequestHandler<GetSubscriptionPlansQuery, Result<List<SubscriptionPlanDto>>>
{
    private readonly IUnitOfWork _uow;

    public GetSubscriptionPlansHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Result<List<SubscriptionPlanDto>>> Handle(GetSubscriptionPlansQuery request, CancellationToken ct)
    {
        var plans = await _uow.PlanDefinitions.GetAllWithFeaturesAsync(ct);

        var dtos = plans.Select(p => new SubscriptionPlanDto(
            p.Id.ToString(),
            p.NameKey,
            p.Price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            p.DurationDays switch { 30 => "monthly", 365 => "yearly", _ => "one_time" },
            p.Description,
            p.IsRecommended,
            p.PlanFeatures.Select(f => new PlanFeatureDto(
                $"{f.Feature.Name}: {f.Value}", 
                !f.Value.Equals("false", StringComparison.OrdinalIgnoreCase))
            ).ToList()
        )).ToList();

        return Result<List<SubscriptionPlanDto>>.Success(dtos);
    }
}
