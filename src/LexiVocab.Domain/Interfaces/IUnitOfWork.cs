namespace LexiVocab.Domain.Interfaces;

/// <summary>
/// Unit of Work pattern — coordinates multiple repository operations in a single transaction.
/// Ensures data consistency when a command modifies multiple entities.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IVocabularyRepository Vocabularies { get; }
    IReviewLogRepository ReviewLogs { get; }
    IMasterVocabularyRepository MasterVocabularies { get; }
    IVocabTagRepository Tags { get; }
    ISubscriptionRepository Subscriptions { get; }
    IPaymentTransactionRepository PaymentTransactions { get; }
    IPlanDefinitionRepository PlanDefinitions { get; }

    /// <summary>
    /// Persist all changes made through repositories as a single atomic transaction.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
    Task ExecuteStrategyAsync(Func<Task> action, CancellationToken ct = default);
}
