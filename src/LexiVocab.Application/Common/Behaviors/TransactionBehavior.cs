using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Common.Behaviors;

/// <summary>
/// Ensures all MediatR commands are executed within a single database transaction.
/// If any part of the command fails, the entire transaction is rolled back.
/// </summary>
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(IUnitOfWork uow, ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Skip transactions for queries (idempotent operations)
        var requestName = typeof(TRequest).Name;
        if (requestName.EndsWith("Query"))
        {
            return await next();
        }

        TResponse? response = default;

        // Execute using the database execution strategy (handles transient failures)
        await _uow.ExecuteStrategyAsync(async () =>
        {
            await _uow.BeginTransactionAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Starting transaction for {RequestName}", requestName);
                
                response = await next();
                
                await _uow.CommitTransactionAsync(cancellationToken);
                
                _logger.LogInformation("Transaction committed for {RequestName}", requestName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transaction failed for {RequestName}. Rolling back...", requestName);
                await _uow.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);

        return response!;
    }
}
