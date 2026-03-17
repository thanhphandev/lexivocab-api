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
            p.Price.ToString("F0", System.Globalization.CultureInfo.InvariantCulture),
            p.DurationDays switch { 30 => "monthly", 365 => "yearly", 0 => "lifetime", _ => "one_time" },
            p.Description,
            p.IsRecommended,
            p.PlanFeatures.Select(f =>
            {
                var included = !f.Value.Equals("false", StringComparison.OrdinalIgnoreCase);
                Dictionary<string, object>? featureParams = null;

                // Map numeric/string values to params so frontend can use them without hard coding
                if (f.Feature.ValueType == "integer" && int.TryParse(f.Value, out var intVal))
                    featureParams = new Dictionary<string, object> { ["count"] = intVal };
                else if (f.Feature.ValueType == "string" && f.Feature.ValueType != "boolean")
                    featureParams = new Dictionary<string, object> { ["value"] = f.Value };

                return new PlanFeatureDto(f.Feature.Code, included, featureParams);
            }).ToList()
        )).ToList();

        return Result<List<SubscriptionPlanDto>>.Success(dtos);
    }
}
