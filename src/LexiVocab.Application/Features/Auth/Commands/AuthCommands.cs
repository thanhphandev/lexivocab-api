using System.Text.Json;
using System.Text.Json.Serialization;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace LexiVocab.Application.Features.Auth.Commands;

// ─── Token Metadata Helper ────────────────────────────────────
public record RefreshTokenMetadata(
    Guid UserId,
    string DeviceInfo,
    string IpAddress,
    DateTime CreatedAt
);

// ─── Register ──────────────────────────────────────────────────
public record RegisterCommand(
    string Email,
    [property: JsonIgnore] string Password,
    string FullName,
    [property: JsonIgnore] string DeviceInfo,
    [property: JsonIgnore] string IpAddress)
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
    private readonly IDistributedCache _cache;

    public RegisterCommandHandler(IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher, IDistributedCache cache)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
        _cache = cache;
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

        var metadata = JsonSerializer.Serialize(new RefreshTokenMetadata(user.Id, request.DeviceInfo, request.IpAddress, DateTime.UtcNow));
        await _cache.SetStringAsync($"rf_token:{refreshToken}", metadata, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) }, ct);

        return Result<AuthResponse>.Created(new AuthResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(),
            accessToken, refreshToken, DateTime.UtcNow.AddHours(1)));
    }
}

// ─── Login ─────────────────────────────────────────────────────
public record LoginCommand(
    string Email,
    [property: JsonIgnore] string Password,
    [property: JsonIgnore] string DeviceInfo,
    [property: JsonIgnore] string IpAddress)
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
    private readonly IDistributedCache _cache;

    public LoginCommandHandler(IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher, IDistributedCache cache)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
        _cache = cache;
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

        var metadata = JsonSerializer.Serialize(new RefreshTokenMetadata(user.Id, request.DeviceInfo, request.IpAddress, DateTime.UtcNow));
        await _cache.SetStringAsync($"rf_token:{refreshToken}", metadata, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) }, ct);

        return Result<AuthResponse>.Success(new AuthResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(),
            accessToken, refreshToken, DateTime.UtcNow.AddHours(1)));
    }
}

// ─── Refresh Token ─────────────────────────────────────────────
public record RefreshTokenCommand(
    string AccessToken,
    [property: JsonIgnore] string RefreshToken,
    [property: JsonIgnore] string DeviceInfo,
    [property: JsonIgnore] string IpAddress)
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
    private readonly IDistributedCache _cache;

    public RefreshTokenCommandHandler(IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher, IDistributedCache cache)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
        _cache = cache;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var cachedTokenData = await _cache.GetStringAsync($"rf_token:{request.RefreshToken}", ct);
        if (string.IsNullOrEmpty(cachedTokenData))
            return Result<AuthResponse>.Unauthorized("Invalid or expired refresh token. It may have been revoked.");

        var metadataStr = JsonSerializer.Deserialize<RefreshTokenMetadata>(cachedTokenData);
        if (metadataStr is null)
            return Result<AuthResponse>.Unauthorized("Invalid token metadata.");

        var userId = metadataStr.UserId;
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        
        if (user is null || !user.IsActive)
            return Result<AuthResponse>.Unauthorized("Account is deactivated or does not exist.");

        // Rotation: delete the old one
        await _cache.RemoveAsync($"rf_token:{request.RefreshToken}", ct);

        var accessToken = _jwt.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var newRefreshToken = _jwt.GenerateRefreshToken();

        user.RefreshTokenHash = _hasher.Hash(newRefreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        var newMetadata = JsonSerializer.Serialize(new RefreshTokenMetadata(user.Id, request.DeviceInfo, request.IpAddress, DateTime.UtcNow));
        await _cache.SetStringAsync($"rf_token:{newRefreshToken}", newMetadata, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) }, ct);

        return Result<AuthResponse>.Success(new AuthResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(),
            accessToken, newRefreshToken, DateTime.UtcNow.AddHours(1)));
    }
}

// ─── Google OAuth Login ────────────────────────────────────────
public record GoogleLoginCommand(
    [property: JsonIgnore] string IdToken,
    [property: JsonIgnore] string DeviceInfo,
    [property: JsonIgnore] string IpAddress)
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
    private readonly IDistributedCache _cache;

    public GoogleLoginCommandHandler(
        IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher, IGoogleAuthService googleAuth, IDistributedCache cache)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
        _googleAuth = googleAuth;
        _cache = cache;
    }

    public async Task<Result<AuthResponse>> Handle(GoogleLoginCommand request, CancellationToken ct)
    {
        var googleUser = await _googleAuth.ValidateIdTokenAsync(request.IdToken, ct);
        if (googleUser is null)
            return Result<AuthResponse>.Unauthorized("Invalid Google ID token.");

        var user = await _uow.Users.GetByAuthProviderAsync("Google", googleUser.Subject, ct);

        if (user is null)
        {
            user = await _uow.Users.GetByEmailAsync(googleUser.Email.ToLowerInvariant(), ct);
            if (user is not null)
            {
                user.AuthProvider = "Google";
                user.AuthProviderId = googleUser.Subject;
            }
            else
            {
                user = new Domain.Entities.User
                {
                    Email = googleUser.Email.ToLowerInvariant(),
                    FullName = googleUser.FullName,
                    AuthProvider = "Google",
                    AuthProviderId = googleUser.Subject,
                    LastLogin = DateTime.UtcNow
                };
                await _uow.Users.AddAsync(user, ct);
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

        var metadata = JsonSerializer.Serialize(new RefreshTokenMetadata(user.Id, request.DeviceInfo, request.IpAddress, DateTime.UtcNow));
        await _cache.SetStringAsync($"rf_token:{refreshToken}", metadata, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) }, ct);

        return Result<AuthResponse>.Success(new AuthResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(),
            accessToken, refreshToken, DateTime.UtcNow.AddHours(1)));
    }
}

// ─── Logout ────────────────────────────────────────────────────
public record LogoutCommand(string RefreshToken) : IRequest<Result>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IDistributedCache _cache;

    public LogoutCommandHandler(IDistributedCache cache) => _cache = cache;

    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(request.RefreshToken))
            await _cache.RemoveAsync($"rf_token:{request.RefreshToken}", ct);
            
        return Result.Success();
    }
}
