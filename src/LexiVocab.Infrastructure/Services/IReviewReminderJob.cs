namespace LexiVocab.Infrastructure.Services;

public interface IReviewReminderJob
{
    Task ExecuteAsync(CancellationToken ct);
}
