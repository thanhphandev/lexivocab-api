using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.AI;

public record StreamStoryQuery(string Word, string? TargetLanguage = null, string? UserLanguage = null, string? Provider = null, string? ModelId = null) 
    : IRequest<Result<IAsyncEnumerable<string>>>, IFeatureGatedRequest
{
    public string FeatureCode => "AI_ACCESS";
    public string? QuotaLimitCode => "AI_DAILY_LIMIT";
}

public class StreamStoryHandler : BaseAIHandler, IRequestHandler<StreamStoryQuery, Result<IAsyncEnumerable<string>>>
{
    private readonly IAIOrchestratorService _aiService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public StreamStoryHandler(IAIOrchestratorService aiService, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _aiService = aiService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IAsyncEnumerable<string>>> Handle(StreamStoryQuery request, CancellationToken ct)
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

        var stream = _aiService.StreamTaskAsync(
            LexiVocab.Domain.Enums.AIUseCase.GenerateStory, 
            parameters, 
            customMapping.Provider ?? request.Provider, 
            customMapping.ModelId ?? request.ModelId, 
            true, 
            customMapping.BaseUrl, 
            customMapping.ApiKey, 
            ct);

        return Result<IAsyncEnumerable<string>>.Success(stream);
    }
}
