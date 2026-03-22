using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.AI;

public record TranslateQuery(string Word, string? Context = null, string? Provider = null, string? ModelId = null, string? From = null, string? To = null, string? CustomBaseUrl = null, string? CustomApiKey = null, string? CustomModel = null) 
    : IRequest<Result<string>>;

public class TranslateHandler : BaseAIHandler, IRequestHandler<TranslateQuery, Result<string>>
{
    private readonly ITranslationStreamService _translationStreamService;
    private readonly IFeatureGatingService _featureGating;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public TranslateHandler(ITranslationStreamService translationStreamService, IFeatureGatingService featureGating, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _translationStreamService = translationStreamService;
        _featureGating = featureGating;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<string>> Handle(TranslateQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);

        string? targetLang = request.To;
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
            return Result<string>.Failure(quotaCheck.Error ?? "Quota exceeded", quotaCheck.StatusCode, quotaCheck.ErrorCode);
        }

        try
        {
            var jsonResult = await _translationStreamService.TranslateAsync(request.Word, request.Context, customMapping.Provider, customMapping.ModelId, request.From, targetLang, customMapping.BaseUrl, customMapping.ApiKey, customMapping.Model, ct);
            
            if (jsonResult != null && jsonResult.Contains("\"error\":"))
            {
                var code = jsonResult.Contains("model", StringComparison.OrdinalIgnoreCase) ? LexiVocab.Domain.Enums.ErrorCode.AI_MODEL_NOT_AVAILABLE : LexiVocab.Domain.Enums.ErrorCode.AI_PROVIDER_ERROR;
                return Result<string>.Failure("AI Provider Error", 502, code);
            }
            return Result<string>.Success(jsonResult ?? string.Empty);
        }
        catch (Exception ex)
        {
            if (ex is System.Net.Http.HttpRequestException || ex is System.TimeoutException)
                return Result<string>.Failure("AI Service is currently unavailable.", 503, LexiVocab.Domain.Enums.ErrorCode.AI_SERVICE_UNAVAILABLE);
                
            return Result<string>.Failure("An unexpected error occurred with the AI provider.", 500, LexiVocab.Domain.Enums.ErrorCode.AI_PROVIDER_ERROR);
        }
    }
}
