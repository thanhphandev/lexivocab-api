using System.Text.Json;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Common.Helpers;
using LexiVocab.Application.DTOs.AI;
using LexiVocab.Domain.Interfaces;
using LexiVocab.Domain.Enums;
using MediatR;

namespace LexiVocab.Application.Features.AI;

public record GetWordQuizQuery(string Word, string? TargetLanguage = null, string? UserLanguage = null, string? Provider = null, string? ModelId = null) 
    : IRequest<Result<QuizDto>>, IFeatureGatedRequest
{
    public string FeatureCode => "QUIZ_GENERATION";
    public string? QuotaLimitCode => "MAX_QUIZ_PER_DAY";
}

public class GetWordQuizHandler : BaseAIHandler, IRequestHandler<GetWordQuizQuery, Result<QuizDto>>
{
    private readonly IAIOrchestratorService _aiService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public GetWordQuizHandler(IAIOrchestratorService aiService, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _aiService = aiService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<QuizDto>> Handle(GetWordQuizQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);

        string tl = string.IsNullOrWhiteSpace(request.TargetLanguage) ? (user?.UserSetting?.TargetLanguage ?? "English") : request.TargetLanguage;
        string ul = string.IsNullOrWhiteSpace(request.UserLanguage)   ? (user?.UserSetting?.NativeLanguage ?? "Vietnamese") : request.UserLanguage;

        var parameters = new Dictionary<string, string>
        {
            { "word", request.Word },
            { "targetLanguage", LanguageMapper.GetName(tl, false) },
            { "userLanguage", LanguageMapper.GetName(ul, false) }
        };

        var customMapping = ResolveCustomProvider(user, request.Provider, request.ModelId, null, null, null);

        try
        {
            var json = await _aiService.ExecuteTaskAsync(
                LexiVocab.Domain.Enums.AIUseCase.GenerateQuiz, 
                parameters, 
                customMapping.Provider ?? request.Provider, 
                customMapping.ModelId ?? request.ModelId, 
                true, 
                customMapping.BaseUrl, 
                customMapping.ApiKey, 
                ct);

            if (string.IsNullOrEmpty(json)) return Result<QuizDto>.Failure("AI failed to generate quiz", 400, LexiVocab.Domain.Enums.ErrorCode.AI_INVALID_REQUEST);

            if (json.Contains("\"error\":"))
            {
                var code = json.Contains("model", StringComparison.OrdinalIgnoreCase) ? LexiVocab.Domain.Enums.ErrorCode.AI_MODEL_NOT_AVAILABLE : LexiVocab.Domain.Enums.ErrorCode.AI_PROVIDER_ERROR;
                return Result<QuizDto>.Failure("AI Provider Error", 502, code);
            }

            try {
                var dto = System.Text.Json.JsonSerializer.Deserialize<QuizDto>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return dto != null ? Result<QuizDto>.Success(dto) : Result<QuizDto>.Failure("AI failed to generate valid quiz", 400, LexiVocab.Domain.Enums.ErrorCode.AI_INVALID_REQUEST);
            } catch {
                return Result<QuizDto>.Failure("AI response was malformed", 400, LexiVocab.Domain.Enums.ErrorCode.AI_INVALID_REQUEST);
            }
        }
        catch (Exception ex)
        {
            if (ex is System.Net.Http.HttpRequestException || ex is System.TimeoutException)
                return Result<QuizDto>.Failure("AI Service is currently unavailable.", 503, LexiVocab.Domain.Enums.ErrorCode.AI_SERVICE_UNAVAILABLE);
                
            return Result<QuizDto>.Failure("An unexpected error occurred with the AI provider.", 500, LexiVocab.Domain.Enums.ErrorCode.AI_PROVIDER_ERROR);
        }
    }
}
