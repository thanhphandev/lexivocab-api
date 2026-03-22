using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Common.Helpers;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.AI;

public record StreamWordExplanationQuery(string Word, string? Context = null, bool AsJson = false, string? TargetLanguage = null, string? UserLanguage = null, string? Provider = null, string? ModelId = null) 
    : IRequest<Result<IAsyncEnumerable<string>>>;

public class StreamWordExplanationHandler : BaseAIHandler, IRequestHandler<StreamWordExplanationQuery, Result<IAsyncEnumerable<string>>>
{
    private readonly IAIOrchestratorService _aiService;
    private readonly IFeatureGatingService _featureGating;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public StreamWordExplanationHandler(IAIOrchestratorService aiService, IFeatureGatingService featureGating, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _aiService = aiService;
        _featureGating = featureGating;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IAsyncEnumerable<string>>> Handle(StreamWordExplanationQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);
        
        string tl = string.IsNullOrWhiteSpace(request.TargetLanguage) ? (user?.UserSetting?.TargetLanguage ?? "English") : request.TargetLanguage;
        string ul = string.IsNullOrWhiteSpace(request.UserLanguage)   ? (user?.UserSetting?.NativeLanguage ?? "Vietnamese") : request.UserLanguage;

        var parameters = new Dictionary<string, string>
        {
            { "word", request.Word },
            { "context", request.Context ?? string.Empty },
            { "targetLanguage", LanguageMapper.GetName(tl, false) },
            { "userLanguage", LanguageMapper.GetName(ul, false) }
        };

        var customMapping = ResolveCustomProvider(user, request.Provider, request.ModelId, null, null, null);

        var quotaCheck = await CheckTranslationQuotaAsync(_featureGating, userId, user, customMapping.Provider ?? request.Provider, customMapping.ModelId ?? request.ModelId, ct, "AI_DAILY_LIMIT");
        if (!quotaCheck.IsSuccess)
        {
            return Result<IAsyncEnumerable<string>>.Failure(quotaCheck.Error ?? "Quota exceeded", quotaCheck.StatusCode, quotaCheck.ErrorCode);
        }

        try
        {
            var stream = _aiService.StreamTaskAsync(
                LexiVocab.Domain.Enums.AIUseCase.ExplainUsage, 
                parameters, 
                customMapping.Provider ?? request.Provider, 
                customMapping.ModelId ?? request.ModelId, 
                request.AsJson, 
                customMapping.BaseUrl, 
                customMapping.ApiKey, 
                ct);

            return Result<IAsyncEnumerable<string>>.Success(stream);
        }
        catch (Exception ex)
        {
            if (ex is System.Net.Http.HttpRequestException || ex is System.TimeoutException)
                return Result<IAsyncEnumerable<string>>.Failure("AI Service is currently unavailable.", 503, LexiVocab.Domain.Enums.ErrorCode.AI_SERVICE_UNAVAILABLE);
                
            return Result<IAsyncEnumerable<string>>.Failure("An unexpected error occurred with the AI provider.", 500, LexiVocab.Domain.Enums.ErrorCode.AI_PROVIDER_ERROR);
        }
    }
}
