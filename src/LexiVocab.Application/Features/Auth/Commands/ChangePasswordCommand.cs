using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.Common.Extensions;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;

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
    private readonly IAuthTokenService _authTokenService;
    private readonly IDateTimeProvider _dateTime;

    public ChangePasswordHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IAuthTokenService authTokenService,
        IDateTimeProvider dateTime)
    {
        _uow = uow;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _authTokenService = authTokenService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        
        if (user == null)
            return Result.NotFound("User account no longer exists.", ErrorCode.RESOURCE_NOT_FOUND);

        if (user.AuthProvider != null)
            return Result.Conflict("Social login accounts cannot change passwords.", ErrorCode.AUTH_SOCIAL_LOGIN_CONFLICT);

        if (string.IsNullOrEmpty(user.PasswordHash) || !_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Result.Failure("Current password is incorrect.", 400, ErrorCode.AUTH_INVALID_CREDENTIALS);

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = _dateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        // Revoke all sessions after password change
        await _authTokenService.RevokeAllSessionsAsync(userId, request.CurrentRefreshToken, ct);

        return Result.Success();
    }
}
