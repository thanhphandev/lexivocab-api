using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace LexiVocab.Application.Features.Auth.Commands;

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
    private readonly IConfiguration _configuration;

    public LoginCommandHandler(IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher, IDistributedCache cache, IConfiguration configuration)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
        _cache = cache;
        _configuration = configuration;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _uow.Users.GetByEmailAsync(request.Email.ToLowerInvariant().Trim(), ct);
        if (user is null || user.PasswordHash is null)
            return Result<AuthResponse>.Unauthorized("Invalid email or password.");

        // Check if account is locked
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            var remainingMinutes = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
            return Result<AuthResponse>.Forbidden($"Account is locked due to too many failed attempts. Please try again in {remainingMinutes} minutes.");
        }

        if (!user.IsActive)
            return Result<AuthResponse>.Forbidden("Account is deactivated.");

        var requireVerification = _configuration["Auth:RequireEmailVerification"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
        if (requireVerification && !user.EmailConfirmed)
            return Result<AuthResponse>.Forbidden("Your email is not verified. Please check your inbox for the verification code or request a new one.");

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                _uow.Users.Update(user);
                await _uow.SaveChangesAsync(ct);
                return Result<AuthResponse>.Forbidden("Account has been locked for 15 minutes due to too many failed login attempts.");
            }
            
            _uow.Users.Update(user);
            await _uow.SaveChangesAsync(ct);
            return Result<AuthResponse>.Unauthorized("Invalid email or password.");
        }

        // Reset lockout on success
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
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
