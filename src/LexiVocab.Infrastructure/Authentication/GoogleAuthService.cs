using System.Net.Http.Json;
using LexiVocab.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Authentication;

/// <summary>
/// Validates Google ID tokens by calling Google's OAuth2 tokeninfo endpoint.
/// Uses typed HttpClient for proper lifecycle management and testability.
/// 
/// Production note: For higher throughput, consider using Google.Apis.Auth
/// which validates tokens locally using Google's public keys (JWKS) instead
/// of making an HTTP call per validation.
/// </summary>
public class GoogleAuthService : IGoogleAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(HttpClient httpClient, ILogger<GoogleAuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<GoogleUserInfo?> ValidateIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}", ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google token validation failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GoogleTokenPayload>(cancellationToken: ct);
            if (payload is null || string.IsNullOrEmpty(payload.Sub))
            {
                _logger.LogWarning("Google token payload missing 'sub' claim");
                return null;
            }

            // Verify email is present and verified
            if (string.IsNullOrEmpty(payload.Email) || payload.EmailVerified != "true")
            {
                _logger.LogWarning("Google token has unverified or missing email");
                return null;
            }

            return new GoogleUserInfo(
                payload.Sub,
                payload.Email,
                payload.Name ?? payload.Email,
                payload.Picture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate Google ID token");
            return null;
        }
    }

    /// <summary>
    /// Maps the JSON response from Google's tokeninfo endpoint.
    /// </summary>
    private sealed record GoogleTokenPayload
    {
        public string? Sub { get; init; }
        public string? Email { get; init; }
        public string? Name { get; init; }
        public string? Picture { get; init; }
        public string? EmailVerified { get; init; }
    }
}
