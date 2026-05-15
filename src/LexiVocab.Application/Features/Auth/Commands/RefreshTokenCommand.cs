using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using LexiVocab.Application.Common;
using LexiVocab.Application.Common.Helpers;
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
    private readonly IDateTimeProvider _dateTime;

    public RefreshTokenCommandHandler(IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher, IDistributedCache cache, IConfiguration configuration, IDateTimeProvider dateTime)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
        _cache = cache;
        _configuration = configuration;
        _dateTime = dateTime;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var cachedTokenData = await _cache.GetStringAsync(RefreshTokenCacheHelper.GetCacheKey(request.RefreshToken), ct);
        if (string.IsNullOrEmpty(cachedTokenData))
        {
            var gracefullyRotatedData = await _cache.GetStringAsync(RefreshTokenCacheHelper.GetGraceCacheKey(request.RefreshToken), ct);
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

        if (string.IsNullOrEmpty(user.RefreshTokenHash) || user.RefreshTokenExpiryTime < _dateTime.UtcNow)
            return Result<AuthResponse>.Unauthorized("Refresh token has been invalidated or expired.", ErrorCode.AUTH_TOKEN_EXPIRED);

        var accessTokenResult = _jwt.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var accessToken = accessTokenResult.Token;
        var accessTokenExpiry = accessTokenResult.ExpiresAt;
        var newRefreshToken = _jwt.GenerateRefreshToken();

        var refreshTokenExpiryDays = int.TryParse(_configuration["Jwt:RefreshTokenExpiryDays"], out var rtDays) ? rtDays : 7;
        var gracePeriodSeconds = int.TryParse(_configuration["Jwt:RefreshTokenGracePeriodSeconds"], out var gpSecs) ? gpSecs : 60;

        user.RefreshTokenHash = _hasher.Hash(newRefreshToken);
        user.RefreshTokenExpiryTime = _dateTime.UtcNow.AddDays(refreshTokenExpiryDays);
        
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        var newMetadata = JsonSerializer.Serialize(new RefreshTokenMetadata(user.Id, request.DeviceInfo, request.IpAddress, _dateTime.UtcNow));
        await _cache.SetStringAsync(RefreshTokenCacheHelper.GetCacheKey(newRefreshToken), newMetadata, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(refreshTokenExpiryDays) }, ct);

        var authResponse = new AuthResponse(
            user.Id, user.Email, user.FullName, user.Role.ToString(),
            accessToken, newRefreshToken, accessTokenExpiry, user.AvatarUrl,
            user.EmailConfirmed, user.IsActive);

        await _cache.SetStringAsync(
            RefreshTokenCacheHelper.GetGraceCacheKey(request.RefreshToken), 
            JsonSerializer.Serialize(authResponse),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(gracePeriodSeconds) }, 
            ct);

        await _cache.RemoveAsync(RefreshTokenCacheHelper.GetCacheKey(request.RefreshToken), ct);

        return Result<AuthResponse>.Success(authResponse);
    }
}
