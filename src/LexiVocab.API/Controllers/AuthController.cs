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
[EnableRateLimiting("AuthLimit")]
public class AuthController : ControllerBase
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

    /// <summary>Register a new account with email and password.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new RegisterCommand(request.Email, request.Password, request.FullName, ClientDevice, ClientIp), ct);
        return ToAuthResult(result);
    }

    /// <summary>Login with email and password. Returns JWT tokens.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(request.Email, request.Password, ClientDevice, ClientIp), ct);
        return ToAuthResult(result);
    }

    /// <summary>Initiate password reset flow by sending a 6-digit code to the email.</summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ForgotPasswordCommand(request.Email), ct);
        return Ok(new { success = true, message = "If the email is registered, a reset code has been sent." });
    }

    /// <summary>Reset password using the 6-digit code received via email.</summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ResetPasswordCommand(request.Email, request.Code, request.NewPassword), ct);
        return ToActionResult(result);
    }

    /// <summary>Verify user's email address using the 6-digit code.</summary>
    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, [FromQuery] string email, CancellationToken ct)
    {
        var result = await _mediator.Send(new VerifyEmailCommand(email, request.Code), ct);
        return ToActionResult(result);
    }

    /// <summary>Login or register with a Google ID token. Auto-links existing email accounts.</summary>
    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new GoogleLoginCommand(request.IdToken, ClientDevice, ClientIp), ct);
        return ToAuthResult(result);
    }

    /// <summary>Refresh an expired access token using a valid refresh token from cookies.</summary>
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

    /// <summary>Logout the user by clearing the refresh token from Redis and deleting the cookie.</summary>
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

    /// <summary>Get the currently authenticated user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCurrentUserQuery(), ct);
        return ToActionResult(result);
    }

    /// <summary>Get the current user's feature permissions and quotas.</summary>
    [HttpGet("permissions")]
    [Authorize]
    [ProducesResponseType(typeof(UserPermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPermissions(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUserPermissionsQuery(), ct);
        return ToActionResult(result);
    }

// ─── Account Management ───────────────────────────────────────

    /// <summary>Update the current user's profile information.</summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateProfileCommand(request.FullName), ct);
        return ToActionResult(result);
    }

    /// <summary>Change the current user's password (for Email/Password accounts only).</summary>
    [HttpPut("me/password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), ct);
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

    /// <summary>Permanently delete the user's account and wipe all associated data.</summary>
    [HttpDelete("me")]
    [Authorize]
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

    /// <summary>Resend verification email for an unverified account.</summary>
    [HttpPost("resend-verification-email")]
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

    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess) return Ok();

        var errorObj = new { error = result.Error };
        return result.StatusCode switch
        {
            400 => BadRequest(errorObj),
            401 => Unauthorized(errorObj),
            403 => StatusCode(403, errorObj),
            404 => NotFound(errorObj),
            409 => Conflict(errorObj),
            _ => StatusCode(500, errorObj)
        };
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });

        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
