using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Application.Features.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using Asp.Versioning;

namespace LexiVocab.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class AuthController : BaseApiController
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    private string ClientDevice => Request.Headers.UserAgent.ToString() ?? "Unknown";
    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

    public AuthController(IMediator mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration;
    }

    /// <summary>
    /// Register a new user account.
    /// </summary>
    /// <remarks>
    /// Creates a new user with email and password. Sends a verification email automatically.
    /// Rate limited to prevent automated registrations.
    /// </remarks>
    /// <param name="request">Registration details (email, password, full name).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Returns the new user details and JWT tokens.</response>
    /// <response code="400">Bad Request: Validation failed.</response>
    /// <response code="409">Conflict: Email already exists.</response>
    /// <response code="429">Too Many Requests: Rate limit exceeded.</response>
    [HttpPost("register")]
    [EnableRateLimiting("AuthStrictLimit")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new RegisterCommand(request.Email, request.Password, request.FullName, ClientDevice, ClientIp), ct);
        return ToAuthResult(result);
    }

    /// <summary>
    /// Authenticate with email and password.
    /// </summary>
    /// <remarks>
    /// Returns access token (JWT) in body and refresh token in a secure HttpOnly cookie.
    /// </remarks>
    /// <param name="request">Login credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns JWT access token and user info.</response>
    /// <response code="401">Unauthorized: Invalid email or password.</response>
    [HttpPost("login")]
    [EnableRateLimiting("AuthStrictLimit")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(request.Email, request.Password, ClientDevice, ClientIp), ct);
        return ToAuthResult(result);
    }

    /// <summary>
    /// Initiate password reset.
    /// </summary>
    /// <remarks>
    /// Sends a 6-digit reset code to the provided email if it exists in our system.
    /// </remarks>
    /// <param name="request">The email address of the account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Success message (always returned for security).</response>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("AuthStrictLimit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ForgotPasswordCommand(request.Email), ct);
        return Ok(new { success = true, message = "If the email is registered, a reset code has been sent." });
    }

    /// <summary>
    /// Reset password using verification code.
    /// </summary>
    /// <param name="request">Code from email and the new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Password reset successful.</response>
    /// <response code="400">Bad Request: Invalid or expired code.</response>
    [HttpPost("reset-password")]
    [EnableRateLimiting("AuthStrictLimit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ResetPasswordCommand(request.Email, request.Code, request.NewPassword), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Verify email address.
    /// </summary>
    /// <param name="request">Verification code.</param>
    /// <param name="email">Email address to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Email verified successfully.</response>
    /// <response code="400">Invalid code.</response>
    [HttpPost("verify-email")]
    [EnableRateLimiting("AuthStrictLimit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, [FromQuery] string email, CancellationToken ct)
    {
        var result = await _mediator.Send(new VerifyEmailCommand(email, request.Code), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Authenticate via Google OAuth.
    /// </summary>
    /// <remarks>
    /// Exchanges a Google ID Token for local JWT tokens. 
    /// Automatically creates an account or links to an existing email.
    /// </remarks>
    /// <param name="request">Google ID Token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Authentication successful.</response>
    /// <response code="401">Invalid Google token.</response>
    [HttpPost("google")]
    [EnableRateLimiting("AuthStrictLimit")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new GoogleLoginCommand(request.IdToken, ClientDevice, ClientIp), ct);
        return ToAuthResult(result);
    }

    /// <summary>
    /// Refresh access token.
    /// </summary>
    /// <remarks>
    /// Uses the refresh token from HttpOnly cookie to issue a new short-lived access token.
    /// </remarks>
    /// <param name="request">The expired access token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">New tokens issued.</response>
    /// <response code="401">Unauthorized: Session expired.</response>
    [HttpPost("refresh")]
    [EnableRateLimiting("RefreshLimit")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return StatusCode(StatusCodes.Status401Unauthorized, new { success = false, error = "Refresh token is missing. Please login again." });

        var result = await _mediator.Send(new RefreshTokenCommand(request.AccessToken, refreshToken, ClientDevice, ClientIp), ct);
        return ToAuthResult(result);
    }

    /// <summary>
    /// Logout current session.
    /// </summary>
    /// <remarks>
    /// Invalidates the refresh token and clears the secure cookie.
    /// </remarks>
    /// <response code="200">Logged out.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _mediator.Send(new LogoutCommand(refreshToken));
        }

        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // We assume production is over HTTPS
            SameSite = SameSiteMode.None
        });
        return Ok(new { success = true, message = "Logged out successfully." });
    }

    /// <summary>
    /// Get current user profile.
    /// </summary>
    /// <response code="200">Returns profile data.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpGet("me")]
    [Authorize]
    [EnableRateLimiting("UserReadLimit")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCurrentUserQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Get user permissions and quotas.
    /// </summary>
    /// <remarks>
    /// Returns dynamic feature flags and current usage limits (e.g., AI quota remaining).
    /// </remarks>
    /// <response code="200">Returns permission set.</response>
    [HttpGet("permissions")]
    [Authorize]
    [EnableRateLimiting("UserReadLimit")]
    [ProducesResponseType(typeof(UserPermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPermissions(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUserPermissionsQuery(), ct);
        return ToActionResult(result);
    }

// ─── Account Management ───────────────────────────────────────

    /// <summary>
    /// Update profile details.
    /// </summary>
    /// <param name="request">New profile data (name, avatar).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Updated profile.</response>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateProfileCommand(request.FullName, request.AvatarUrl), ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Change password.
    /// </summary>
    /// <remarks>
    /// For email/password accounts only. Invalidates all other sessions.
    /// </remarks>
    /// <param name="request">Current and new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Password updated.</response>
    /// <response code="409">Conflict: Invalid current password.</response>
    [HttpPut("me/password")]
    [Authorize]
    [EnableRateLimiting("SensitiveWriteLimit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var currentRefreshToken = Request.Cookies["refreshToken"];
        var result = await _mediator.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword, currentRefreshToken), ct);
        if (result.IsSuccess)
        {
            // Revoke current session cookie since we invalidated the token hash.
            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            });
            return Ok(new { success = true, message = "Password changed successfully. Please log in again." });
        }
        return ToActionResult(result);
    }

    /// <summary>
    /// Delete user account.
    /// </summary>
    /// <remarks>
    /// Irreversible operation. Deletes all vocabulary, settings, and review history.
    /// </remarks>
    /// <response code="200">Account deleted.</response>
    [HttpDelete("me")]
    [Authorize]
    [EnableRateLimiting("SensitiveWriteLimit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteAccountCommand(), ct);
        if (result.IsSuccess)
        {
            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            });
            return Ok(new { success = true, message = "Account deleted successfully." });
        }
        return ToActionResult(result);
    }

    /// <summary>
    /// Revoke all active sessions.
    /// </summary>
    /// <remarks>
    /// Signs out the user from all devices (Chrome extensions, web, etc.)
    /// </remarks>
    /// <response code="200">All sessions revoked.</response>
    [HttpPost("revoke-all-sessions")]
    [Authorize]
    [EnableRateLimiting("SensitiveWriteLimit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeAllSessions(CancellationToken ct)
    {
        var currentRefreshToken = Request.Cookies["refreshToken"];
        var result = await _mediator.Send(new RevokeAllSessionsCommand(currentRefreshToken), ct);
        if (result.IsSuccess)
        {
            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            });
            return Ok(new { success = true, message = "All sessions revoked. Please log in again." });
        }
        return ToActionResult(result);
    }

    /// <summary>
    /// Resend verification email.
    /// </summary>
    /// <param name="request">Email address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Verification email sent.</response>
    [HttpPost("resend-verification-email")]
    [EnableRateLimiting("AuthStrictLimit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendVerificationEmail([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        // ForgotPasswordRequest has 'Email', which is exactly what we need
        var result = await _mediator.Send(new ResendVerificationEmailCommand(request.Email), ct);
        return ToActionResult(result);
    }

    private IActionResult ToAuthResult(Result<AuthResponse> result)
    {
        if (result.IsSuccess && result.Data is not null)
        {
            if (!string.IsNullOrEmpty(result.Data.RefreshToken))
            {
                SetRefreshTokenCookie(result.Data.RefreshToken);
            }
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        }

        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var refreshTokenExpiryDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Ensure it's transmitted over HTTPS only
            SameSite = SameSiteMode.None, // Support both Chrome Extensions and Next.js
            Expires = DateTime.UtcNow.AddDays(refreshTokenExpiryDays)
        };
        // Ensure consistency with the reader in RefreshToken / Logout
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

}
