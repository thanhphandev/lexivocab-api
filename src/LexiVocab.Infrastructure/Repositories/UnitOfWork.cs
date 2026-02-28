using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Persistence;

namespace LexiVocab.Infrastructure.Repositories;

/// <summary>
/// Unit of Work implementation — coordinates all repositories and commits as a single transaction.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public IUserRepository Users { get; }
    public IVocabularyRepository Vocabularies { get; }
    public IReviewLogRepository ReviewLogs { get; }
    public IMasterVocabularyRepository MasterVocabularies { get; }
    public ISubscriptionRepository Subscriptions { get; }
    public IPaymentTransactionRepository PaymentTransactions { get; }

    public UnitOfWork(
        AppDbContext context,
        IUserRepository users,
        IVocabularyRepository vocabularies,
        IReviewLogRepository reviewLogs,
        IMasterVocabularyRepository masterVocabularies,
        ISubscriptionRepository subscriptions,
        IPaymentTransactionRepository paymentTransactions)
    {
        _context = context;
        Users = users;
        Vocabularies = vocabularies;
        ReviewLogs = reviewLogs;
        MasterVocabularies = masterVocabularies;
        Subscriptions = subscriptions;
        PaymentTransactions = paymentTransactions;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public void Dispose()
        => _context.Dispose();
}
