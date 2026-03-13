using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

public interface IFeatureDefinitionRepository : IRepository<FeatureDefinition>
{
    Task<FeatureDefinition?> GetByCodeAsync(string code, CancellationToken ct = default);
}
