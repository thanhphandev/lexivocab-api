using LexiVocab.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Application.Common.Behaviors;

public class FeatureGatingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IFeatureGatedRequest
    where TResponse : LexiVocab.Application.Common.IResult<TResponse>
{
    private readonly IFeatureGatingService _featureGating;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<FeatureGatingBehavior<TRequest, TResponse>> _logger;

    public FeatureGatingBehavior(IFeatureGatingService featureGating, ICurrentUserService currentUser, ILogger<FeatureGatingBehavior<TRequest, TResponse>> logger)
    {
        _featureGating = featureGating;
        _currentUser = currentUser;
        _logger = logger;
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
                return CreateFailureResponse("Feature not available in your current plan. Please upgrade to Pro.", LexiVocab.Domain.Enums.ErrorCode.AUTHZ_INSUFFICIENT_PERMISSIONS);
            }
        }
        else
        {
            // Quota check
            if (!await _featureGating.ConsumeQuotaAsync(userId.Value, request.FeatureCode, request.QuotaLimitCode, cancellationToken))
            {
                _logger.LogInformation("Feature gating: User {UserId} exceeded quota for feature {FeatureCode} (Limit: {LimitCode})", userId, request.FeatureCode, request.QuotaLimitCode);
                var errorCode = (request.FeatureCode == "AI_ACCESS" || request.FeatureCode == "QUIZ_GENERATION") ? LexiVocab.Domain.Enums.ErrorCode.AI_QUOTA_EXCEEDED : LexiVocab.Domain.Enums.ErrorCode.VOCAB_QUOTA_EXCEEDED;
                return CreateFailureResponse("Daily limit reached or feature not available. Upgrade to Pro for higher limits.", errorCode);
            }
        }

        return await next(cancellationToken);
    }

    private TResponse CreateFailureResponse(string message, LexiVocab.Domain.Enums.ErrorCode errorCode)
    {
        return TResponse.Failure(message, 403, errorCode);
    }
}
