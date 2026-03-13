using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Auth.Commands;

public record LogoutCommand(string RefreshToken) : IRequest<Result>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public LogoutCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(request.RefreshToken))
            await _cache.RemoveAsync($"rf_token:{request.RefreshToken}", ct);

        // Also invalidate in DB to prevent reuse after Redis cache expires if leaked
        if (_currentUser.UserId.HasValue)
        {
            var user = await _uow.Users.GetByIdAsync(_currentUser.UserId.Value, ct);
            if (user != null)
            {
                user.RefreshTokenHash = null;
                user.RefreshTokenExpiryTime = null;
                _uow.Users.Update(user);
                await _uow.SaveChangesAsync(ct);
            }
        }
            
        return Result.Success();
    }
}
