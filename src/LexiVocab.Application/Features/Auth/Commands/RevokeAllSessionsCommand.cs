using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Auth.Commands;

/// <summary>
/// Revokes all active sessions for the current user across all devices.
/// Clears both the DB refresh token hash and the current Redis key.
/// Note: other devices' Redis keys will expire naturally (max 7 days),
/// but DB hash is cleared so RefreshToken validation fails immediately.
/// </summary>
public record RevokeAllSessionsCommand(string? CurrentRefreshToken = null) : IRequest<Result>;

public class RevokeAllSessionsHandler : IRequestHandler<RevokeAllSessionsCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public RevokeAllSessionsHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result> Handle(RevokeAllSessionsCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return Result.NotFound("User not found in context.");

        var user = await _uow.Users.GetByIdAsync(userId.Value, ct);
        if (user == null)
            return Result.NotFound("User account no longer exists.");

        // Evict the current session's Redis key immediately so it cannot be reused.
        // Other devices' Redis keys will fail DB hash validation on next refresh attempt.
        if (!string.IsNullOrEmpty(request.CurrentRefreshToken))
            await _cache.RemoveAsync($"rf_token:{request.CurrentRefreshToken}", ct);

        // Clearing the DB hash invalidates ALL sessions — any device trying to refresh
        // will fail the BCrypt.Verify check against a null hash.
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiryTime = null;
        user.UpdatedAt = DateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
