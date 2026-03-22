using System.Text.Json;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.AI;
using LexiVocab.Domain.Interfaces;
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
            { "targetLanguage", tl },
            { "userLanguage", ul }
        };

        var customMapping = ResolveCustomProvider(user, request.Provider, request.ModelId, null, null, null);

        var json = await _aiService.ExecuteTaskAsync(
            LexiVocab.Domain.Enums.AIUseCase.GenerateQuiz, 
            parameters, 
            customMapping.Provider ?? request.Provider, 
            customMapping.ModelId ?? request.ModelId, 
            true, 
            customMapping.BaseUrl, 
            customMapping.ApiKey, 
            ct);

        if (string.IsNullOrEmpty(json)) return Result<QuizDto>.Failure("AI failed to generate quiz");

        try {
            var dto = JsonSerializer.Deserialize<QuizDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto != null ? Result<QuizDto>.Success(dto) : Result<QuizDto>.Failure("AI failed to generate valid quiz");
        } catch {
            return Result<QuizDto>.Failure("AI response was malformed");
        }
    }
}
