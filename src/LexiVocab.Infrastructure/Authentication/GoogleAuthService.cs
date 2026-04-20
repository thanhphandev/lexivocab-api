using Google.Apis.Auth;
using LexiVocab.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexiVocab.Infrastructure.Authentication;

/// <summary>
/// Validates Google ID tokens using Google's public keys (JWKS).
/// This implementation performs local validation without making an HTTP call per request,
/// offering higher throughput and improved security.
/// </summary>
public class GoogleAuthService : IGoogleAuthService
{
    private readonly ILogger<GoogleAuthService> _logger;
    private readonly string? _expectedClientId;

    public GoogleAuthService(IConfiguration config, ILogger<GoogleAuthService> logger)
    {
        _logger = logger;
        _expectedClientId = config["Google:ClientId"];
    }

    public async Task<GoogleUserInfo?> ValidateIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_expectedClientId) || _expectedClientId == "REPLACE_WITH_GOOGLE_CLIENT_ID")
            {
                _logger.LogError("Google:ClientId is not configured in appsettings.json");
                return null;
            }

            var allowedAudiences = _expectedClientId
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(id => id.Trim())
                .ToArray();

            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = allowedAudiences
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            if (payload is null)
            {
                _logger.LogWarning("Google token validation returned null payload");
                return null;
            }

            // Verify email is present and verified
            if (string.IsNullOrEmpty(payload.Email) || !payload.EmailVerified)
            {
                _logger.LogWarning("Google token has unverified or missing email");
                return null;
            }

            return new GoogleUserInfo(
                payload.Subject,
                payload.Email,
                payload.Name ?? payload.Email,
                payload.Picture);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Invalid Google ID token provided");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Google ID token validation");
            return null;
        }
    }
}
