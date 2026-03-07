namespace LexiVocab.Infrastructure.Services;

public interface ISubscriptionExpirationJob
{
    Task ExecuteAsync(CancellationToken ct);
}
