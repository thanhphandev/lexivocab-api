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
    private readonly IConfiguration _configuration;

    public RefreshTokenCommandHandler(IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher, IDistributedCache cache, IConfiguration configuration)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
        _cache = cache;
        _configuration = configuration;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var cachedTokenData = await _cache.GetStringAsync($"rf_token:{request.RefreshToken}", ct);
        if (string.IsNullOrEmpty(cachedTokenData))
        {
            var gracefullyRotatedData = await _cache.GetStringAsync($"rf_token_grace:{request.RefreshToken}", ct);
            if (!string.IsNullOrEmpty(gracefullyRotatedData))
            {
                var savedResponse = JsonSerializer.Deserialize<AuthResponse>(gracefullyRotatedData);
                if (savedResponse is not null)
                    return Result<AuthResponse>.Success(savedResponse);
            }

            return Result<AuthResponse>.Unauthorized("Invalid or expired refresh token. It may have been revoked.", ErrorCode.AUTH_SESSION_EXPIRED);
        }

        var metadataStr = JsonSerializer.Deserialize<RefreshTokenMetadata>(cachedTokenData);
        if (metadataStr is null)
            return Result<AuthResponse>.Unauthorized("Invalid token metadata.", ErrorCode.AUTH_REFRESH_TOKEN_INVALID);

        var userId = metadataStr.UserId;
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        
        if (user is null || !user.IsActive)
            return Result<AuthResponse>.Unauthorized("Account is deactivated or does not exist.", ErrorCode.AUTH_ACCOUNT_DISABLED);

        if (string.IsNullOrEmpty(user.RefreshTokenHash) || user.RefreshTokenExpiryTime < DateTime.UtcNow)
            return Result<AuthResponse>.Unauthorized("Refresh token has been invalidated or expired.", ErrorCode.AUTH_TOKEN_EXPIRED);

        var accessTokenResult = _jwt.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var accessToken = accessTokenResult.Token;
        var accessTokenExpiry = accessTokenResult.ExpiresAt;
        var newRefreshToken = _jwt.GenerateRefreshToken();

        var refreshTokenExpiryDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");
        var gracePeriodSeconds = int.Parse(_configuration["Jwt:RefreshTokenGracePeriodSeconds"] ?? "60");

        user.RefreshTokenHash = _hasher.Hash(newRefreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);
        
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        var newMetadata = JsonSerializer.Serialize(new RefreshTokenMetadata(user.Id, request.DeviceInfo, request.IpAddress, DateTime.UtcNow));
        await _cache.SetStringAsync($"rf_token:{newRefreshToken}", newMetadata, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(refreshTokenExpiryDays) }, ct);

        var authResponse = new AuthResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(),
            accessToken, newRefreshToken, accessTokenExpiry, user.AvatarUrl,
            user.EmailConfirmed, user.IsActive);

        await _cache.SetStringAsync(
            $"rf_token_grace:{request.RefreshToken}", 
            JsonSerializer.Serialize(authResponse),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(gracePeriodSeconds) }, 
            ct);

        await _cache.RemoveAsync($"rf_token:{request.RefreshToken}", ct);

        return Result<AuthResponse>.Success(authResponse);
    }
}
