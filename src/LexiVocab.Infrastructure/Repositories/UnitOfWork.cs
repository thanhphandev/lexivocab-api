using LexiVocab.Domain.Interfaces;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LexiVocab.Infrastructure.Repositories;

/// <summary>
/// Unit of Work implementation — coordinates all repositories and commits as a single transaction.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public IUserRepository Users { get; }
    public IVocabularyRepository Vocabularies { get; }
    public IReviewLogRepository ReviewLogs { get; }
    public IMasterVocabularyRepository MasterVocabularies { get; }
    public IVocabTagRepository Tags { get; }
    public ISubscriptionRepository Subscriptions { get; }
    public IPaymentTransactionRepository PaymentTransactions { get; }
    public IPlanDefinitionRepository PlanDefinitions { get; }

    public UnitOfWork(
        AppDbContext context,
        IUserRepository users,
        IVocabularyRepository vocabularies,
        IReviewLogRepository reviewLogs,
        IMasterVocabularyRepository masterVocabularies,
        IVocabTagRepository tags,
        ISubscriptionRepository subscriptions,
        IPaymentTransactionRepository paymentTransactions,
        IPlanDefinitionRepository planDefinitions)
    {
        _context = context;
        Users = users;
        Vocabularies = vocabularies;
        ReviewLogs = reviewLogs;
        MasterVocabularies = masterVocabularies;
        Tags = tags;
        Subscriptions = subscriptions;
        PaymentTransactions = paymentTransactions;
        PlanDefinitions = planDefinitions;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction != null) return;
        _currentTransaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            if (_currentTransaction != null) await _currentTransaction.CommitAsync(ct);
        }
        finally
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        try
        {
            if (_currentTransaction != null) await _currentTransaction.RollbackAsync(ct);
        }
        finally
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }

    public async Task ExecuteStrategyAsync(Func<Task> action, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(action);
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
    }
}
