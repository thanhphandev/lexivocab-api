using LexiVocab.Application.Common;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Application.Features.Auth.Commands;
using LexiVocab.Application.Features.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LexiVocab.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[EnableRateLimiting("AuthLimit")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    private string ClientDevice => Request.Headers.UserAgent.ToString() ?? "Unknown";
    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

    public AuthController(IMediator mediator) => _mediator = mediator;

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

    private IActionResult ToAuthResult(Result<AuthResponse> result)
    {
        if (result.IsSuccess && result.Data is not null)
        {
            SetRefreshTokenCookie(result.Data.RefreshToken);
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        }

        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Ensure it's transmitted over HTTPS only
            SameSite = SameSiteMode.None, // Support both Chrome Extensions and Next.js
            Expires = DateTime.UtcNow.AddDays(7)
        };
        // Ensure consistency with the reader in RefreshToken / Logout
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });

        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
