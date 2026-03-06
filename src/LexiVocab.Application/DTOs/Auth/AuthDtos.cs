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
    string AccessToken);

public record AuthResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string AccessToken,
    [property: JsonIgnore] string RefreshToken,
    DateTime ExpiresAt);

public record UserProfileDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLogin);
