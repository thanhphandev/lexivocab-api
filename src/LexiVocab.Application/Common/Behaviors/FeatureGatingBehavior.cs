using LexiVocab.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Common.Behaviors;

public class FeatureGatingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IFeatureGatedRequest
{
    private readonly IFeatureGatingService _featureGating;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<FeatureGatingBehavior<TRequest, TResponse>> _logger;

    public FeatureGatingBehavior(IFeatureGatingService featureGating, ICurrentUserService currentUser, ILogger<FeatureGatingBehavior<TRequest, TResponse>> _logger)
    {
        _featureGating = featureGating;
        _currentUser = currentUser;
        this._logger = _logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
        {
            _logger.LogWarning("Feature gating failed: User is not authenticated.");
            throw new UnauthorizedAccessException("Authentication required.");
        }

        if (string.IsNullOrEmpty(request.QuotaLimitCode))
        {
            // Simple feature check
            if (!await _featureGating.HasFeatureAsync(userId.Value, request.FeatureCode, cancellationToken))
            {
                _logger.LogInformation("Feature gating: User {UserId} does not have access to feature {FeatureCode}", userId, request.FeatureCode);
                return CreateFailureResponse("Feature not available in your current plan. Please upgrade to Pro.");
            }
        }
        else
        {
            // Quota check
            if (!await _featureGating.ConsumeQuotaAsync(userId.Value, request.FeatureCode, request.QuotaLimitCode, cancellationToken))
            {
                _logger.LogInformation("Feature gating: User {UserId} exceeded quota for feature {FeatureCode} (Limit: {LimitCode})", userId, request.FeatureCode, request.QuotaLimitCode);
                return CreateFailureResponse("Daily limit reached or feature not available. Upgrade to Pro for higher limits.");
            }
        }

        return await next();
    }

    private TResponse CreateFailureResponse(string message)
    {
        // Result<T> is the standard response type in this project
        var responseType = typeof(TResponse);
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var dataPropertyName = responseType.GetGenericArguments()[0];
            var failureMethod = typeof(Result<>).MakeGenericType(dataPropertyName).GetMethod("Failure", [typeof(string), typeof(int)]);
            return (TResponse)failureMethod!.Invoke(null, [message, 403])!;
        }
        
        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(message, 403);
        }

        throw new InvalidOperationException($"FeatureGatingBehavior requires a response of type Result or Result<T>, but got {responseType.Name}.");
    }
}
