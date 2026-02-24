namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// JWT token generation and validation service.
/// </summary>
public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId, string email, string role);
    string GenerateRefreshToken();
    Guid? ValidateAccessToken(string token);
}
