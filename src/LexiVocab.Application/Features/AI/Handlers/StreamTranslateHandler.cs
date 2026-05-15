using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.AI;

public record StreamTranslateQuery(string Word, string? Context = null, string? Provider = null, string? ModelId = null, string? From = null, string? To = null, string? CustomBaseUrl = null, string? CustomApiKey = null, string? CustomModel = null) 
    : IRequest<Result<IAsyncEnumerable<string>>>;

public class StreamTranslateHandler : BaseAIHandler, IRequestHandler<StreamTranslateQuery, Result<IAsyncEnumerable<string>>>
{
    private readonly ITranslationStreamService _translationStreamService;
    private readonly IFeatureGatingService _featureGating;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public StreamTranslateHandler(ITranslationStreamService translationStreamService, IFeatureGatingService featureGating, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _translationStreamService = translationStreamService;
        _featureGating = featureGating;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IAsyncEnumerable<string>>> Handle(StreamTranslateQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();
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
            return Result<IAsyncEnumerable<string>>.Failure(quotaCheck.Error ?? "Quota exceeded", quotaCheck.StatusCode, quotaCheck.ErrorCode);
        }

        try
        {
            var stream = _translationStreamService.StreamTranslateAsync(request.Word, request.Context, customMapping.Provider, customMapping.ModelId, request.From, targetLang, customMapping.BaseUrl, customMapping.ApiKey, customMapping.Model, ct);
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
