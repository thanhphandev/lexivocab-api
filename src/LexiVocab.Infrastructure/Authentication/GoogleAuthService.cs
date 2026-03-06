using System.Net.Http.Json;
using LexiVocab.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
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
    private readonly string? _expectedClientId;

    public GoogleAuthService(HttpClient httpClient, IConfiguration config, ILogger<GoogleAuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _expectedClientId = config["Google:ClientId"];
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

            // Verify audience (aud) matches our Google Client ID to prevent token reuse attacks
            if (!string.IsNullOrEmpty(_expectedClientId) && payload.Aud != _expectedClientId)
            {
                _logger.LogWarning(
                    "Google token audience mismatch. Expected: {Expected}, Got: {Actual}",
                    _expectedClientId, payload.Aud);
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
        public string? Aud { get; init; }
    }
}
