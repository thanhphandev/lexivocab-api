using FluentValidation;
using MediatR;

namespace LexiVocab.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that auto-validates all requests before handlers execute.
/// Collects all FluentValidation errors and returns a structured failure result.
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        // Execute sequentially to prevent EF Core concurrency exceptions in custom MustAsync rules
        var validationResults = new List<FluentValidation.Results.ValidationResult>();
        foreach (var validator in _validators)
        {
            validationResults.Add(await validator.ValidateAsync(context, cancellationToken));
        }

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
        {
            var errorMessage = string.Join("; ", failures.Select(f => f.ErrorMessage));
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
