using System.Text.Json.Serialization;

namespace LexiVocab.Application.DTOs.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string FullName);

public record LoginRequest(
    string Email,
    string Password);

public record GoogleLoginRequest(
    string IdToken);

public record RefreshTokenRequest(
    string AccessToken,
    string? RefreshToken = null);

public record AuthResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAt,
    string? AvatarUrl = null);

public record UserProfileDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLogin,
    string? AvatarUrl = null);

public record UpdateProfileRequest(
    string FullName,
    string? AvatarUrl = null);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Code, string NewPassword);

public record VerifyEmailRequest(string Code);
