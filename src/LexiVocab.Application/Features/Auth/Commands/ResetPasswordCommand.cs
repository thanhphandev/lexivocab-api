using System;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Auth.Commands;

public record ResetPasswordCommand(string Email, string Code, string NewPassword) : IRequest<Result>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.UserUpdated;
    public string? EntityType => "User";
}

public class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IDistributedCache _cache;
    private readonly IDateTimeProvider _dateTime;

    public ResetPasswordHandler(IUnitOfWork uow, IPasswordHasher hasher, IDistributedCache cache, IDateTimeProvider dateTime)
    {
        _uow = uow;
        _hasher = hasher;
        _cache = cache;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var email = request.Email.ToLowerInvariant().Trim();
        var cacheKey = $"reset-pass:{email}";

        var savedCode = await _cache.GetStringAsync(cacheKey, ct);
        if (string.IsNullOrEmpty(savedCode) || savedCode != request.Code)
            return Result.Failure("Invalid or expired reset code.", 400, ErrorCode.AUTH_VERIFICATION_CODE_EXPIRED);

        var user = await _uow.Users.GetByEmailAsync(email, ct);
        if (user == null)
            return Result.NotFound("User no longer exists.", ErrorCode.RESOURCE_NOT_FOUND);

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        user.RefreshTokenHash = null; // Force logout on all devices
        user.RefreshTokenExpiryTime = null;
        user.UpdatedAt = _dateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        await _cache.RemoveAsync(cacheKey, ct);

        return Result.Success();
    }
}
