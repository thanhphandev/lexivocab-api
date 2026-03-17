namespace LexiVocab.Infrastructure.Services;

public interface IPendingPaymentCleanupJob
{
    Task ExecuteAsync(CancellationToken ct);
}

