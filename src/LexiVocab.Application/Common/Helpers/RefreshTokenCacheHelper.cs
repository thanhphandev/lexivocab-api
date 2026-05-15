using System.Security.Cryptography;
using System.Text;

namespace LexiVocab.Application.Common.Helpers;

/// <summary>
/// Provides secure cache key generation for refresh tokens.
/// Refresh tokens are hashed with SHA256 before being used as Redis/cache keys,
/// preventing token leakage if the cache store is compromised.
/// </summary>
public static class RefreshTokenCacheHelper
{
    private const string Prefix = "rf_token:";
    private const string GracePrefix = "rf_token_grace:";

    /// <summary>
    /// Generates a cache key for a refresh token using SHA256 hash.
    /// The raw token is never stored as a cache key.
    /// </summary>
    public static string GetCacheKey(string refreshToken)
        => Prefix + HashToken(refreshToken);

    /// <summary>
    /// Generates a grace-period cache key for a refresh token (used during token rotation
    /// to handle multi-tab race conditions).
    /// </summary>
    public static string GetGraceCacheKey(string refreshToken)
        => GracePrefix + HashToken(refreshToken);

    private static string HashToken(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
