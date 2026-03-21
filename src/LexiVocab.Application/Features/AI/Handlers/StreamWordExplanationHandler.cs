using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.AI;

public record StreamWordExplanationQuery(string Word, string? Context = null, bool AsJson = false, string? TargetLanguage = null, string? UserLanguage = null, string? Provider = null, string? ModelId = null) 
    : IRequest<Result<IAsyncEnumerable<string>>>, IFeatureGatedRequest
{
    public string FeatureCode => "AI_ACCESS";
    public string? QuotaLimitCode => "AI_DAILY_LIMIT";
}

public class StreamWordExplanationHandler : IRequestHandler<StreamWordExplanationQuery, Result<IAsyncEnumerable<string>>>
{
    private readonly IAIOrchestratorService _aiService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public StreamWordExplanationHandler(IAIOrchestratorService aiService, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _aiService = aiService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IAsyncEnumerable<string>>> Handle(StreamWordExplanationQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);
        
        string tl = string.IsNullOrWhiteSpace(request.TargetLanguage) ? (user?.UserSetting?.TargetLanguage ?? "English") : request.TargetLanguage;
        string ul = string.IsNullOrWhiteSpace(request.UserLanguage)   ? (user?.UserSetting?.NativeLanguage ?? "Vietnamese") : request.UserLanguage;

        var parameters = new Dictionary<string, string>
        {
            { "word", request.Word },
            { "context", request.Context ?? string.Empty },
            { "targetLanguage", tl },
            { "userLanguage", ul }
        };

        var stream = _aiService.StreamTaskAsync(LexiVocab.Domain.Enums.AIUseCase.ExplainUsage, parameters, request.Provider, request.ModelId, request.AsJson, null, null, ct);
        return Result<IAsyncEnumerable<string>>.Success(stream);
    }
}
