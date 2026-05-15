using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Auth.Commands;

public record LogoutCommand(string RefreshToken) : IRequest<Result>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IAuthTokenService _authTokenService;
    private readonly ICurrentUserService _currentUser;

    public LogoutCommandHandler(IAuthTokenService authTokenService, ICurrentUserService currentUser)
    {
        _authTokenService = authTokenService;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        await _authTokenService.RevokeRefreshTokenAsync(request.RefreshToken, _currentUser.UserId, ct);
        return Result.Success();
    }
}
