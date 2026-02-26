namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// Google OAuth ID token validation service.
/// Validates tokens via Google's tokeninfo endpoint and extracts user info.
/// </summary>
public interface IGoogleAuthService
{
    Task<GoogleUserInfo?> ValidateIdTokenAsync(string idToken, CancellationToken ct = default);
}

/// <summary>
/// Verified Google user information extracted from a valid ID token.
/// </summary>
public record GoogleUserInfo(
    string Subject,     // Google unique user ID (sub claim)
    string Email,
    string FullName,
    string? PictureUrl);
