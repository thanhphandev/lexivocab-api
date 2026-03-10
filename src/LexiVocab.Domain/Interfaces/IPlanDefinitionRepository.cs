using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

public interface IPlanDefinitionRepository : IRepository<PlanDefinition>
{
    Task<IReadOnlyList<PlanDefinition>> GetAllWithFeaturesAsync(CancellationToken ct = default);
    Task<PlanDefinition?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<PlanDefinition?> GetByNameWithFeaturesAsync(string name, CancellationToken ct = default);
}
