using System.Text.Json;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.AI;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.AI;

public record GetWordExplanationQuery(string Word, string? Context = null) : IRequest<Result<WordExplanationDto>>;

public record GetRelatedWordsQuery(string Word) : IRequest<Result<RelatedWordsDto>>;

public record GetWordQuizQuery(string Word) : IRequest<Result<QuizDto>>;

public class AIHandlers : 
    IRequestHandler<GetWordExplanationQuery, Result<WordExplanationDto>>,
    IRequestHandler<GetRelatedWordsQuery, Result<RelatedWordsDto>>,
    IRequestHandler<GetWordQuizQuery, Result<QuizDto>>
{
    private readonly IAIService _aiService;
    private readonly IFeatureGatingService _featureGating;
    private readonly ICurrentUserService _currentUser;

    public AIHandlers(IAIService aiService, IFeatureGatingService featureGating, ICurrentUserService currentUser)
    {
        _aiService = aiService;
        _featureGating = featureGating;
        _currentUser = currentUser;
    }

    public async Task<Result<WordExplanationDto>> Handle(GetWordExplanationQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        if (!await _featureGating.ConsumeQuotaAsync(userId, "ADVANCED_AI", "AI_DAILY_LIMIT", ct))
            return Result<WordExplanationDto>.Failure("Daily AI request limit reached or feature not available. Upgrade to Pro for higher limits.", 403);

        var json = await _aiService.ExplainUsageAsync(request.Word, request.Context, ct);
        if (string.IsNullOrEmpty(json)) return Result<WordExplanationDto>.Failure("AI failed to generate explanation");

        try {
            var dto = JsonSerializer.Deserialize<WordExplanationDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto != null ? Result<WordExplanationDto>.Success(dto) : Result<WordExplanationDto>.Failure("AI failed to generate valid explanation");
        } catch {
            return Result<WordExplanationDto>.Failure("AI response was malformed");
        }
    }

    public async Task<Result<RelatedWordsDto>> Handle(GetRelatedWordsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        if (!await _featureGating.ConsumeQuotaAsync(userId, "ADVANCED_AI", "AI_DAILY_LIMIT", ct))
            return Result<RelatedWordsDto>.Failure("Daily AI request limit reached or feature not available. Upgrade to Pro for higher limits.", 403);

        var json = await _aiService.GetRelatedWordsAsync(request.Word, ct);
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
        if (!await _featureGating.ConsumeQuotaAsync(userId, "QUIZ_GENERATION", "AI_DAILY_LIMIT", ct))
            return Result<QuizDto>.Failure("Daily AI request limit reached or feature not available. Upgrade to Pro for higher limits.", 403);

        var json = await _aiService.GenerateQuizAsync(request.Word, ct);
        if (string.IsNullOrEmpty(json)) return Result<QuizDto>.Failure("AI failed to generate quiz");

        try {
            var dto = JsonSerializer.Deserialize<QuizDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto != null ? Result<QuizDto>.Success(dto) : Result<QuizDto>.Failure("AI failed to generate valid quiz");
        } catch {
            return Result<QuizDto>.Failure("AI response was malformed");
        }
    }
}
