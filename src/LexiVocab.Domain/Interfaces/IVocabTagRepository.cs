using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

public interface IVocabTagRepository : IRepository<VocabTag>
{
    Task<IEnumerable<VocabTag>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<VocabTag?> GetBySlugAsync(Guid userId, string slug, CancellationToken ct = default);
    Task<VocabTag> GetOrCreateByDomainAsync(Guid userId, string domain, CancellationToken ct = default);
    Task IncrementWordCountAsync(Guid tagId, int count = 1, CancellationToken ct = default);
    Task DecrementWordCountAsync(Guid tagId, int count = 1, CancellationToken ct = default);
}
