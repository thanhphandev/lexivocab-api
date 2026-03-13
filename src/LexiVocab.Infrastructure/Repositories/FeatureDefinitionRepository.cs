using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Repositories;

public class FeatureDefinitionRepository : GenericRepository<FeatureDefinition>, IFeatureDefinitionRepository
{
    public FeatureDefinitionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<FeatureDefinition?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.Code == code, ct);
    }
}
