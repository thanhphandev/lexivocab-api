using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Admin.Commands;

public record UpdateUserStatusCommand(Guid UserId, bool IsActive) : IRequest<Result<string>>;

public class UpdateUserStatusHandler : IRequestHandler<UpdateUserStatusCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IDistributedCache _cache;

    public UpdateUserStatusHandler(IUnitOfWork uow, ICurrentUserService currentUser, IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result<string>> Handle(UpdateUserStatusCommand request, CancellationToken ct)
    {
        // Get target user
        var targetUser = await _uow.Users.GetByIdAsync(request.UserId, ct);
        if (targetUser == null) return Result<string>.Failure("User not found", 404);

        // Security: Prevention of Admin-on-Admin action and self-lockout
        var currentAdminId = _currentUser.UserId;
        
        if (targetUser.Id == currentAdminId)
            return Result<string>.Failure("You cannot change your own status.", 403);

        if (targetUser.Role == LexiVocab.Domain.Enums.UserRole.Admin)
            return Result<string>.Failure("You cannot deactivate or modify another administrator.", 403);

        targetUser.IsActive = request.IsActive;
        targetUser.UpdatedAt = DateTime.UtcNow;

        _uow.Users.Update(targetUser);
        await _uow.SaveChangesAsync(ct);

        var redisKey = $"user:deactivated:{request.UserId}";
        if (!request.IsActive)
        {
            await _cache.SetStringAsync(redisKey, "true", new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) }, ct);
        }
        else
        {
            await _cache.RemoveAsync(redisKey, ct);
        }

        var statusString = request.IsActive ? "Activated" : "Deactivated";
        return Result<string>.Success($"User successfully {statusString}");
    }
}
