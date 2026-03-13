using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace LexiVocab.Application.Features.Auth.Commands;

public record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword
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
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
    }
}

public class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;

    public ChangePasswordHandler(IUnitOfWork uow, ICurrentUserService currentUser, IPasswordHasher passwordHasher)
    {
        _uow = uow;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return Result.NotFound("User not found in context.");

        var user = await _uow.Users.GetByIdAsync(userId.Value, ct);
        if (user == null)
            return Result.NotFound("User account no longer exists.");

        if (user.AuthProvider != null)
            return Result.Conflict("Social login accounts cannot change passwords.");

        if (string.IsNullOrEmpty(user.PasswordHash) || !_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Result.Failure("Current password is incorrect.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        
        // Revoke all existing sessions by regenerating the refresh token hash or clearing it entirely.
        // For security, we'll force the user to re-login on other devices next time token expires.
        user.RefreshTokenHash = null; 
        user.RefreshTokenExpiryTime = null;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
