using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Auth.Commands;

// ─── Register ──────────────────────────────────────────────────
public record RegisterCommand(string Email, string Password, string FullName)
    : IRequest<Result<AuthResponse>>;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;

    public RegisterCommandHandler(IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken ct)
    {
        if (await _uow.Users.EmailExistsAsync(request.Email, ct))
            return Result<AuthResponse>.Conflict($"Email '{request.Email}' is already registered.");

        var user = new Domain.Entities.User
        {
            Email = request.Email.ToLowerInvariant().Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            LastLogin = DateTime.UtcNow
        };

        await _uow.Users.AddAsync(user, ct);

        // Auto-create default settings for new user
        user.UserSetting = new Domain.Entities.UserSetting { UserId = user.Id };

        await _uow.SaveChangesAsync(ct);

        var accessToken = _jwt.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var refreshToken = _jwt.GenerateRefreshToken();

        user.RefreshTokenHash = _hasher.Hash(refreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _uow.SaveChangesAsync(ct);

        return Result<AuthResponse>.Created(new AuthResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(),
            accessToken, refreshToken, DateTime.UtcNow.AddHours(1)));
    }
}

// ─── Login ─────────────────────────────────────────────────────
public record LoginCommand(string Email, string Password)
    : IRequest<Result<AuthResponse>>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;

    public LoginCommandHandler(IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByEmailAsync(request.Email.ToLowerInvariant().Trim(), ct);
        if (user is null || user.PasswordHash is null)
            return Result<AuthResponse>.Unauthorized("Invalid email or password.");

        if (!_hasher.Verify(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Unauthorized("Invalid email or password.");

        if (!user.IsActive)
            return Result<AuthResponse>.Forbidden("Account is deactivated.");

        user.LastLogin = DateTime.UtcNow;

        var accessToken = _jwt.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var refreshToken = _jwt.GenerateRefreshToken();

        user.RefreshTokenHash = _hasher.Hash(refreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(new AuthResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(),
            accessToken, refreshToken, DateTime.UtcNow.AddHours(1)));
    }
}

// ─── Refresh Token ─────────────────────────────────────────────
public record RefreshTokenCommand(Guid UserId, string RefreshToken)
    : IRequest<Result<AuthResponse>>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;

    public RefreshTokenCommandHandler(IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
        if (user is null || user.RefreshTokenHash is null || user.RefreshTokenExpiryTime < DateTime.UtcNow)
            return Result<AuthResponse>.Unauthorized("Invalid or expired refresh token.");

        if (!_hasher.Verify(request.RefreshToken, user.RefreshTokenHash))
            return Result<AuthResponse>.Unauthorized("Invalid refresh token.");

        var accessToken = _jwt.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var newRefreshToken = _jwt.GenerateRefreshToken();

        user.RefreshTokenHash = _hasher.Hash(newRefreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(new AuthResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(),
            accessToken, newRefreshToken, DateTime.UtcNow.AddHours(1)));
    }
}
