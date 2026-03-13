namespace LexiVocab.Infrastructure.Services;

public interface IMasterVocabularyUpdateJob
{
    Task ExecuteAsync(CancellationToken ct);
}
