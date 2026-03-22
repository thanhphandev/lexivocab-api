using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Auth.Commands;

public record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string? CurrentRefreshToken = null
) : IRequest<Result>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.UserUpdated;
    public string? EntityType => "User";
}

public class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.").WithErrorCode(ErrorCode.AUTH_PASSWORD_TOO_WEAK.ToString())
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.").WithErrorCode(ErrorCode.AUTH_PASSWORD_TOO_WEAK.ToString())
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.").WithErrorCode(ErrorCode.AUTH_PASSWORD_TOO_WEAK.ToString())
            .Matches(@"\d").WithMessage("Password must contain at least one digit.").WithErrorCode(ErrorCode.AUTH_PASSWORD_TOO_WEAK.ToString());
    }
}

public class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDistributedCache _cache;

    public ChangePasswordHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IDistributedCache cache)
    {
        _uow = uow;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _cache = cache;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return Result.NotFound("User not found in context.", ErrorCode.RESOURCE_NOT_FOUND);

        var user = await _uow.Users.GetByIdAsync(userId.Value, ct);
        if (user == null)
            return Result.NotFound("User account no longer exists.", ErrorCode.RESOURCE_NOT_FOUND);

        if (user.AuthProvider != null)
            return Result.Conflict("Social login accounts cannot change passwords.", ErrorCode.AUTH_SOCIAL_LOGIN_CONFLICT);

        if (string.IsNullOrEmpty(user.PasswordHash) || !_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Result.Failure("Current password is incorrect.", 400, ErrorCode.AUTH_INVALID_CREDENTIALS);

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        // Revoke all sessions: clear DB hash AND evict current Redis key so attacker
        // cannot reuse a stolen refresh token for the remaining TTL window.
        if (!string.IsNullOrEmpty(request.CurrentRefreshToken))
            await _cache.RemoveAsync($"rf_token:{request.CurrentRefreshToken}", ct);

        user.RefreshTokenHash = null;
        user.RefreshTokenExpiryTime = null;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
