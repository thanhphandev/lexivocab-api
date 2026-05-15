using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Entities;

namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// High-level service for issuing and managing authentication token pairs (Access + Refresh).
/// Centralizes token persistence in DB and Cache to ensure consistency across different auth flows.
/// </summary>
public interface IAuthTokenService
{
    /// <summary>
    /// Generates a new Access + Refresh token pair for the user, 
    /// persists the refresh token hash to the database, and saves session metadata to the cache.
    /// </summary>
    Task<AuthResponse> IssueTokenPairAsync(User user, string deviceInfo, string ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Revokes a specific refresh token from both Cache and Database.
    /// </summary>
    Task RevokeRefreshTokenAsync(string refreshToken, Guid? userId = null, CancellationToken ct = default);

    /// <summary>
    /// Revokes ALL refresh tokens for the user by clearing the DB hash.
    /// Also optionally removes the current session's token from cache.
    /// </summary>
    Task RevokeAllSessionsAsync(Guid userId, string? currentRefreshToken = null, CancellationToken ct = default);
}
