using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Repositories;

public class PlanPricingRepository : GenericRepository<PlanPricing>, IPlanPricingRepository
{
    public PlanPricingRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PlanPricing?> GetByIdWithPlanAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(p => p.Plan)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }
}
