using System.Text.Json;
using LexiVocab.Application.Common.Helpers;
using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace LexiVocab.Infrastructure.Authentication;

public class AuthTokenService : IAuthTokenService
{
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;

    public AuthTokenService(
        IJwtTokenService jwt,
        IPasswordHasher hasher,
        IUnitOfWork uow,
        IDistributedCache cache,
        IConfiguration configuration,
        IDateTimeProvider dateTime)
    {
        _jwt = jwt;
        _hasher = hasher;
        _uow = uow;
        _cache = cache;
        _configuration = configuration;
        _dateTime = dateTime;
    }

    private readonly IDateTimeProvider _dateTime;

    public async Task<AuthResponse> IssueTokenPairAsync(User user, string deviceInfo, string ipAddress, CancellationToken ct = default)
    {
        var accessTokenResult = _jwt.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var refreshToken = _jwt.GenerateRefreshToken();

        // Use safe parsing for configuration
        var refreshTokenExpiryDays = int.TryParse(_configuration["Jwt:RefreshTokenExpiryDays"], out var rtDays) ? rtDays : 7;

        // Persist to DB
        user.RefreshTokenHash = _hasher.Hash(refreshToken);
        user.RefreshTokenExpiryTime = _dateTime.UtcNow.AddDays(refreshTokenExpiryDays);
        user.LastLogin = _dateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        // Persist metadata to Cache (securely hashed key)
        var metadata = JsonSerializer.Serialize(new RefreshTokenMetadata(user.Id, deviceInfo, ipAddress, _dateTime.UtcNow));
        var cacheOptions = new DistributedCacheEntryOptions 
        { 
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(refreshTokenExpiryDays) 
        };

        await _cache.SetStringAsync(RefreshTokenCacheHelper.GetCacheKey(refreshToken), metadata, cacheOptions, ct);

        return new AuthResponse(
            user.Id, 
            user.Email, 
            user.FullName, 
            user.Role.ToString(),
            accessTokenResult.Token, 
            refreshToken, 
            accessTokenResult.ExpiresAt, 
            user.AvatarUrl,
            user.EmailConfirmed, 
            user.IsActive);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, Guid? userId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken)) return;

        // 1. Remove from Cache
        await _cache.RemoveAsync(RefreshTokenCacheHelper.GetCacheKey(refreshToken), ct);

        // 2. Remove from DB if userId is provided
        if (userId.HasValue)
        {
            var user = await _uow.Users.GetByIdAsync(userId.Value, ct);
            if (user != null)
            {
                user.RefreshTokenHash = null;
                user.RefreshTokenExpiryTime = null;
                _uow.Users.Update(user);
                await _uow.SaveChangesAsync(ct);
            }
        }
    }

    public async Task RevokeAllSessionsAsync(Guid userId, string? currentRefreshToken = null, CancellationToken ct = default)
    {
        // 1. Remove current session from Cache if provided
        if (!string.IsNullOrEmpty(currentRefreshToken))
        {
            await _cache.RemoveAsync(RefreshTokenCacheHelper.GetCacheKey(currentRefreshToken), ct);
        }

        // 2. Clear DB hash to invalidate ALL sessions
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user != null)
        {
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiryTime = null;
            _uow.Users.Update(user);
            await _uow.SaveChangesAsync(ct);
        }
    }
}
