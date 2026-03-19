using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Repositories;

public class PlanDefinitionRepository : GenericRepository<PlanDefinition>, IPlanDefinitionRepository
{
    public PlanDefinitionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<PlanDefinition>> GetAllWithFeaturesAsync(CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(p => p.Pricings)
            .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(ct);
    }

    public async Task<PlanDefinition?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == name, ct);
    }

    public async Task<PlanDefinition?> GetByNameWithFeaturesAsync(string name, CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
            .FirstOrDefaultAsync(p => p.Name == name, ct);
    }

    public async Task<PlanDefinition?> GetByIdWithFeaturesAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(p => p.Pricings)
            .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }
}
