using System.Text.Json;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.AI;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.AI;

public record GetRelatedWordsQuery(string Word, string? TargetLanguage = null, string? UserLanguage = null) : IRequest<Result<RelatedWordsDto>>;

public record GetWordQuizQuery(string Word, string? TargetLanguage = null, string? UserLanguage = null) : IRequest<Result<QuizDto>>;

public record StreamWordExplanationQuery(string Word, string? Context = null, bool AsJson = false, string? TargetLanguage = null, string? UserLanguage = null) : IRequest<Result<IAsyncEnumerable<string>>>;

public record StreamTranslateQuery(string Word, string? Context = null, string? Provider = null, string? ModelId = null, string? From = null, string? To = null, string? CustomBaseUrl = null, string? CustomApiKey = null, string? CustomModel = null) : IRequest<Result<IAsyncEnumerable<string>>>;

public record TranslateQuery(string Word, string? Context = null, string? Provider = null, string? ModelId = null, string? From = null, string? To = null, string? CustomBaseUrl = null, string? CustomApiKey = null, string? CustomModel = null) : IRequest<Result<string>>;

public class AIHandlers : 
    IRequestHandler<GetRelatedWordsQuery, Result<RelatedWordsDto>>,
    IRequestHandler<GetWordQuizQuery, Result<QuizDto>>,
    IRequestHandler<StreamWordExplanationQuery, Result<IAsyncEnumerable<string>>>,
    IRequestHandler<StreamTranslateQuery, Result<IAsyncEnumerable<string>>>,
    IRequestHandler<TranslateQuery, Result<string>>
{
    private readonly IAIService _aiService;
    private readonly ITranslationStreamService _translationStreamService;
    private readonly IFeatureGatingService _featureGating;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public AIHandlers(IAIService aiService, ITranslationStreamService translationStreamService, IFeatureGatingService featureGating, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _aiService = aiService;
        _translationStreamService = translationStreamService;
        _featureGating = featureGating;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IAsyncEnumerable<string>>> Handle(StreamWordExplanationQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue) 
            return Result<IAsyncEnumerable<string>>.Failure("Authentication required.", 401);
        
        if (!await _featureGating.ConsumeQuotaAsync(userId.Value, "AI_ACCESS", "AI_DAILY_LIMIT", ct))
        {
            return Result<IAsyncEnumerable<string>>.Failure("Daily AI request limit reached. Upgrade to Pro for higher limits.", 403);
        }
        
        var user = await _unitOfWork.Users.GetByIdAsync(userId.Value, ct);
        string tl = string.IsNullOrWhiteSpace(request.TargetLanguage) ? (user?.UserSetting?.TargetLanguage ?? "English") : request.TargetLanguage;
        string ul = string.IsNullOrWhiteSpace(request.UserLanguage)   ? (user?.UserSetting?.NativeLanguage ?? "Vietnamese") : request.UserLanguage;

        var stream = _aiService.StreamExplainUsageAsync(request.Word, request.Context, request.AsJson, tl, ul, ct);
        return Result<IAsyncEnumerable<string>>.Success(stream);
    }

    private (string? Provider, string? ModelId, string? BaseUrl, string? ApiKey, string? Model) ResolveCustomProvider(LexiVocab.Domain.Entities.User? user, string? originalProvider, string? originalModelId, string? originalBaseUrl, string? originalApiKey, string? originalModel)
    {
        string? resolvedProvider = originalProvider;
        string? resolvedModelId = originalModelId;
        string? resolvedBaseUrl = originalBaseUrl;
        string? resolvedApiKey = originalApiKey;
        string? resolvedModel = originalModel;

        if (user?.UserSetting != null && !string.IsNullOrWhiteSpace(user.UserSetting.CustomLlmsJson) && !string.IsNullOrWhiteSpace(originalProvider))
        {
            try {
                using var doc = JsonDocument.Parse(user.UserSetting.CustomLlmsJson);
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("id", out var idProp) && idProp.GetString() == originalProvider)
                    {
                        resolvedProvider = "custom";
                        if (element.TryGetProperty("baseUrl", out var baseUrlProp)) resolvedBaseUrl = baseUrlProp.GetString();
                        if (element.TryGetProperty("apiKey", out var apiKeyProp)) resolvedApiKey = apiKeyProp.GetString();
                        if (element.TryGetProperty("model", out var modelProp)) {
                            resolvedModel = modelProp.GetString();
                            resolvedModelId = modelProp.GetString();
                        }
                        break;
                    }
                }
            } catch { }
        }
        return (resolvedProvider, resolvedModelId, resolvedBaseUrl, resolvedApiKey, resolvedModel);
    }

    private async Task<Result<bool>> CheckTranslationQuotaAsync(Guid userId, LexiVocab.Domain.Entities.User? user, string? provider, CancellationToken ct)
    {
        string p = provider ?? "cloudflare";
        if (p == "custom" || p.StartsWith("google", StringComparison.OrdinalIgnoreCase) || p.StartsWith("bing", StringComparison.OrdinalIgnoreCase) || p.StartsWith("lingva", StringComparison.OrdinalIgnoreCase) || p.StartsWith("chrome-ai", StringComparison.OrdinalIgnoreCase))
        {
            return Result<bool>.Success(true);
        }

        bool isProModel = false;
        var permissions = await _featureGating.GetPermissionsAsync(userId, ct);
        if (permissions.FeatureFlags.TryGetValue("AVAILABLE_LLM_MODELS", out var modelsJson))
        {
            try {
                using var doc = JsonDocument.Parse(modelsJson);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("id", out var idProp) && idProp.GetString() == p)
                    {
                        if (el.TryGetProperty("isPro", out var isProProp) && isProProp.ValueKind == JsonValueKind.True)
                            isProModel = true;
                        break;
                    }
                }
            } catch { }
        }

        if (isProModel && !await _featureGating.HasFeatureAsync(userId, "ADVANCED_AI", ct))
        {
            return Result<bool>.Failure("This AI model requires an active Premium subscription.", 403);
        }

        if (!await _featureGating.ConsumeQuotaAsync(userId, "AI_ACCESS", "LLM_TRANSLATION_LIMIT", ct))
        {
            return Result<bool>.Failure("Daily LLM translation limit reached. Upgrade to Pro for high volume translations.", 403);
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<IAsyncEnumerable<string>>> Handle(StreamTranslateQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue) 
            return Result<IAsyncEnumerable<string>>.Failure("Authentication required.", 401);
        
        var user = await _unitOfWork.Users.GetByIdAsync(userId.Value, ct);
        string? targetLang = request.To;
        if (string.IsNullOrWhiteSpace(targetLang) || targetLang == "auto")
        {
            if (user?.UserSetting != null && !string.IsNullOrWhiteSpace(user.UserSetting.TargetLanguage))
            {
                targetLang = user.UserSetting.TargetLanguage;
            }
        }
        
        var customMapping = ResolveCustomProvider(user, request.Provider, request.ModelId, request.CustomBaseUrl, request.CustomApiKey, request.CustomModel);
        
        var quotaCheck = await CheckTranslationQuotaAsync(userId.Value, user, customMapping.Provider ?? request.Provider, ct);
        if (!quotaCheck.IsSuccess)
        {
            return Result<IAsyncEnumerable<string>>.Failure(quotaCheck.Error ?? "Quota exceeded", quotaCheck.StatusCode);
        }

        // 2. Call Service for streaming Translation
        var stream = _translationStreamService.StreamTranslateAsync(request.Word, request.Context, customMapping.Provider, customMapping.ModelId, request.From, targetLang, customMapping.BaseUrl, customMapping.ApiKey, customMapping.Model, ct);
        return Result<IAsyncEnumerable<string>>.Success(stream);
    }

    public async Task<Result<string>> Handle(TranslateQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue) 
            return Result<string>.Failure("Authentication required.", 401);
        
        var user = await _unitOfWork.Users.GetByIdAsync(userId.Value, ct);
        string? targetLang = request.To;
        if (string.IsNullOrWhiteSpace(targetLang) || targetLang == "auto")
        {
            if (user?.UserSetting != null && !string.IsNullOrWhiteSpace(user.UserSetting.TargetLanguage))
            {
                targetLang = user.UserSetting.TargetLanguage;
            }
        }
        
        var customMapping = ResolveCustomProvider(user, request.Provider, request.ModelId, request.CustomBaseUrl, request.CustomApiKey, request.CustomModel);
        
        var quotaCheck = await CheckTranslationQuotaAsync(userId.Value, user, customMapping.Provider ?? request.Provider, ct);
        if (!quotaCheck.IsSuccess)
        {
            return Result<string>.Failure(quotaCheck.Error ?? "Quota exceeded", quotaCheck.StatusCode);
        }

        var jsonResult = await _translationStreamService.TranslateAsync(request.Word, request.Context, customMapping.Provider, customMapping.ModelId, request.From, targetLang, customMapping.BaseUrl, customMapping.ApiKey, customMapping.Model, ct);
        return Result<string>.Success(jsonResult);
    }


    public async Task<Result<RelatedWordsDto>> Handle(GetRelatedWordsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        if (!await _featureGating.ConsumeQuotaAsync(userId, "AI_ACCESS", "AI_DAILY_LIMIT", ct))
            return Result<RelatedWordsDto>.Failure("Daily AI request limit reached or feature not available. Upgrade to Pro for higher limits.", 403);

        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);
        string tl = string.IsNullOrWhiteSpace(request.TargetLanguage) ? (user?.UserSetting?.TargetLanguage ?? "English") : request.TargetLanguage;
        string ul = string.IsNullOrWhiteSpace(request.UserLanguage)   ? (user?.UserSetting?.NativeLanguage ?? "Vietnamese") : request.UserLanguage;

        var json = await _aiService.GetRelatedWordsAsync(request.Word, tl, ul, ct);
        if (string.IsNullOrEmpty(json)) return Result<RelatedWordsDto>.Failure("AI failed to suggest related words");

        try {
            var dto = JsonSerializer.Deserialize<RelatedWordsDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto != null ? Result<RelatedWordsDto>.Success(dto) : Result<RelatedWordsDto>.Failure("AI failed to suggest valid related words");
        } catch {
            return Result<RelatedWordsDto>.Failure("AI response was malformed");
        }
    }

    public async Task<Result<QuizDto>> Handle(GetWordQuizQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        if (!await _featureGating.ConsumeQuotaAsync(userId, "QUIZ_GENERATION", "MAX_QUIZ_PER_DAY", ct))
            return Result<QuizDto>.Failure("Daily quiz limit reached or feature not available. Upgrade to Pro for higher limits.", 403);

        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);
        string tl = string.IsNullOrWhiteSpace(request.TargetLanguage) ? (user?.UserSetting?.TargetLanguage ?? "English") : request.TargetLanguage;
        string ul = string.IsNullOrWhiteSpace(request.UserLanguage)   ? (user?.UserSetting?.NativeLanguage ?? "Vietnamese") : request.UserLanguage;

        var json = await _aiService.GenerateQuizAsync(request.Word, tl, ul, ct);
        if (string.IsNullOrEmpty(json)) return Result<QuizDto>.Failure("AI failed to generate quiz");

        try {
            var dto = JsonSerializer.Deserialize<QuizDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto != null ? Result<QuizDto>.Success(dto) : Result<QuizDto>.Failure("AI failed to generate valid quiz");
        } catch {
            return Result<QuizDto>.Failure("AI response was malformed");
        }
    }
}
