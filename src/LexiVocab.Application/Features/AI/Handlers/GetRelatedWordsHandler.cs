using System.Text.Json;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.AI;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.AI;

public record GetRelatedWordsQuery(string Word, string? TargetLanguage = null, string? UserLanguage = null, string? Provider = null, string? ModelId = null) 
    : IRequest<Result<RelatedWordsDto>>, IFeatureGatedRequest
{
    public string FeatureCode => "AI_ACCESS";
    public string? QuotaLimitCode => "AI_DAILY_LIMIT";
}

public class GetRelatedWordsHandler : IRequestHandler<GetRelatedWordsQuery, Result<RelatedWordsDto>>
{
    private readonly IAIOrchestratorService _aiService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public GetRelatedWordsHandler(IAIOrchestratorService aiService, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _aiService = aiService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RelatedWordsDto>> Handle(GetRelatedWordsQuery request, CancellationToken ct)
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

        var json = await _aiService.ExecuteTaskAsync(LexiVocab.Domain.Enums.AIUseCase.SuggestRelated, parameters, request.Provider, request.ModelId, true, null, null, ct);
        if (string.IsNullOrEmpty(json)) return Result<RelatedWordsDto>.Failure("AI failed to suggest related words");

        try {
            var dto = JsonSerializer.Deserialize<RelatedWordsDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dto != null ? Result<RelatedWordsDto>.Success(dto) : Result<RelatedWordsDto>.Failure("AI failed to suggest valid related words");
        } catch {
            return Result<RelatedWordsDto>.Failure("AI response was malformed");
        }
    }
}
