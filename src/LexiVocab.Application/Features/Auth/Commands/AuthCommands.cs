using System.Text.Json.Serialization;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;

namespace LexiVocab.Application.Features.Auth.Commands;

// ─── Register ──────────────────────────────────────────────────
public record RegisterCommand(
    string Email,
    [property: JsonIgnore] string Password,
    string FullName)
    : IRequest<Result<AuthResponse>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.Register;
    public string? EntityType => "User";
}

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
public record LoginCommand(
    string Email,
    [property: JsonIgnore] string Password)
    : IRequest<Result<AuthResponse>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.Login;
    public string? EntityType => "User";
}

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
public record RefreshTokenCommand(
    Guid UserId,
    [property: JsonIgnore] string RefreshToken)
    : IRequest<Result<AuthResponse>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.TokenRefresh;
    public string? EntityType => "User";
}

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

// ─── Google OAuth Login ────────────────────────────────────────
public record GoogleLoginCommand(
    [property: JsonIgnore] string IdToken)
    : IRequest<Result<AuthResponse>>, IAuditedRequest
{
    public AuditAction AuditAction => AuditAction.GoogleLogin;
    public string? EntityType => "User";
}

public class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;
    private readonly IGoogleAuthService _googleAuth;

    public GoogleLoginCommandHandler(
        IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher, IGoogleAuthService googleAuth)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
        _googleAuth = googleAuth;
    }

    public async Task<Result<AuthResponse>> Handle(GoogleLoginCommand request, CancellationToken ct)
    {
        var googleUser = await _googleAuth.ValidateIdTokenAsync(request.IdToken, ct);
        if (googleUser is null)
            return Result<AuthResponse>.Unauthorized("Invalid Google ID token.");

        // 1. Check if user already exists with this Google account
        var user = await _uow.Users.GetByAuthProviderAsync("Google", googleUser.Subject, ct);

        if (user is null)
        {
            // 2. Check if email already registered with email/password — link accounts
            user = await _uow.Users.GetByEmailAsync(googleUser.Email.ToLowerInvariant(), ct);
            if (user is not null)
            {
                // Link Google provider to existing email account
                user.AuthProvider = "Google";
                user.AuthProviderId = googleUser.Subject;
            }
            else
            {
                // 3. Brand new user — register with Google
                user = new Domain.Entities.User
                {
                    Email = googleUser.Email.ToLowerInvariant(),
                    FullName = googleUser.FullName,
                    AuthProvider = "Google",
                    AuthProviderId = googleUser.Subject,
                    LastLogin = DateTime.UtcNow
                };

                await _uow.Users.AddAsync(user, ct);

                // Auto-create default settings
                user.UserSetting = new Domain.Entities.UserSetting { UserId = user.Id };
            }
        }

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
