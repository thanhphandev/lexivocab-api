using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
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
        var userId = _currentUser.UserId!.Value;
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
            return Result<IAsyncEnumerable<string>>.Failure(quotaCheck.Error ?? "Quota exceeded", quotaCheck.StatusCode);
        }

        var parameters = new Dictionary<string, string>
        {
            { "Text", request.Text },
            { "TargetLanguage", targetLang ?? "English" },
            { "Style", string.IsNullOrWhiteSpace(request.Style) ? "Natural and accurate" : request.Style }
        };

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
}
