using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Auth.Commands;

public record DeleteAccountCommand : IRequest<Result>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.UserDeleted;
    public string? EntityType => "User";
}

public class DeleteAccountHandler : IRequestHandler<DeleteAccountCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly Microsoft.Extensions.Caching.Distributed.IDistributedCache _cache;

    public DeleteAccountHandler(IUnitOfWork uow, ICurrentUserService currentUser, Microsoft.Extensions.Caching.Distributed.IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result> Handle(DeleteAccountCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return Result.NotFound("User not found in context.");

        var user = await _uow.Users.GetByIdAsync(userId.Value, ct);
        if (user == null)
            return Result.NotFound("User account no longer exists.");

        _uow.Users.Remove(user);
        await _uow.SaveChangesAsync(ct);

        // Revoke active JWT tokens
        await _cache.SetStringAsync($"user:deactivated:{userId.Value}", "true", 
            new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) }, ct);

        return Result.Success();
    }
}
