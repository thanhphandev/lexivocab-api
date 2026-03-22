namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// JWT token generation result containing token and expiration time.
/// </summary>
public record TokenResult(string Token, DateTime ExpiresAt);

/// <summary>
/// JWT token generation and validation service.
/// </summary>
public interface IJwtTokenService
{
    TokenResult GenerateAccessToken(Guid userId, string email, string role);
    TokenResult GenerateImpersonationToken(Guid userId, string email, string role, Guid impersonatorId);
    string GenerateRefreshToken();
    Guid? ValidateAccessToken(string token);
    Guid? GetUserIdFromExpiredToken(string token);
}
