using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

public interface IPlanPricingRepository : IRepository<PlanPricing>
{
    Task<PlanPricing?> GetByIdWithPlanAsync(Guid id, CancellationToken ct = default);
}
