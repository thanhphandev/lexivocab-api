using LexiVocab.Domain.Entities;

namespace LexiVocab.Domain.Interfaces;

/// <summary>
/// Master vocabulary lookup repository.
/// </summary>
public interface IMasterVocabularyRepository : IRepository<MasterVocabulary>
{
    Task<MasterVocabulary?> GetByWordAsync(string word, CancellationToken ct = default);
    Task<IReadOnlyList<MasterVocabulary>> SearchAsync(string query, int limit = 10, CancellationToken ct = default);
}
