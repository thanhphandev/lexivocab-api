using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Common.Helpers;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LexiVocab.Application.Features.AI;

public record StreamInputTranslateQuery(string Text, string? TargetLanguage = null, string? Style = null, string? Provider = null, string? ModelId = null, string? CustomBaseUrl = null, string? CustomApiKey = null, string? CustomModel = null) 
    : IRequest<Result<IAsyncEnumerable<string>>>;

public class StreamInputTranslateHandler : BaseAIHandler, IRequestHandler<StreamInputTranslateQuery, Result<IAsyncEnumerable<string>>>
{
    private readonly IAIOrchestratorService _orchestrator;
    private readonly IFeatureGatingService _featureGating;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public StreamInputTranslateHandler(IAIOrchestratorService orchestrator, IFeatureGatingService featureGating, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _orchestrator = orchestrator;
        _featureGating = featureGating;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IAsyncEnumerable<string>>> Handle(StreamInputTranslateQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);
        
        string? targetLang = request.TargetLanguage;
        if (string.IsNullOrWhiteSpace(targetLang) || targetLang == "auto")
        {
            if (user?.UserSetting != null && !string.IsNullOrWhiteSpace(user.UserSetting.TargetLanguage))
            {
                targetLang = user.UserSetting.TargetLanguage;
            }
        }
        
        var customMapping = ResolveCustomProvider(user, request.Provider, request.ModelId, request.CustomBaseUrl, request.CustomApiKey, request.CustomModel);
        
        var quotaCheck = await CheckTranslationQuotaAsync(_featureGating, userId, user, customMapping.Provider ?? request.Provider, customMapping.ModelId ?? request.ModelId, ct);
        if (!quotaCheck.IsSuccess)
        {
            return Result<IAsyncEnumerable<string>>.Failure(quotaCheck.Error ?? "Quota exceeded", quotaCheck.StatusCode, quotaCheck.ErrorCode);
        }

        var parameters = new Dictionary<string, string>
        {
            { "Text", request.Text },
            { "TargetLanguage", LanguageMapper.GetName(targetLang ?? "English", false) },
            { "Style", string.IsNullOrWhiteSpace(request.Style) ? "Natural and accurate" : request.Style }
        };

        try
        {
            var stream = _orchestrator.StreamTaskAsync(
                AIUseCase.InputTranslation, 
                parameters, 
                customMapping.Provider ?? request.Provider ?? "openrouter", 
                customMapping.ModelId ?? request.ModelId, 
                false, 
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
