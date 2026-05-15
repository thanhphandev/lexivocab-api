using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Common.Extensions;
using MediatR;

namespace LexiVocab.Application.Features.Auth.Commands;

public record RevokeAllSessionsCommand(string? CurrentRefreshToken = null) : IRequest<Result>;

public class RevokeAllSessionsHandler : IRequestHandler<RevokeAllSessionsCommand, Result>
{
    private readonly IAuthTokenService _authTokenService;
    private readonly ICurrentUserService _currentUser;

    public RevokeAllSessionsHandler(IAuthTokenService authTokenService, ICurrentUserService currentUser)
    {
        _authTokenService = authTokenService;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(RevokeAllSessionsCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();
        await _authTokenService.RevokeAllSessionsAsync(userId, request.CurrentRefreshToken, ct);
        return Result.Success();
    }
}
