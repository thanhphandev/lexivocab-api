using LexiVocab.Domain.Common;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// Core user entity. Uses UUID to prevent ID enumeration and enable sharding.
/// Passwords stored as BCrypt/Argon2 hashes — never plaintext.
/// </summary>
public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateTime? LastLogin { get; set; }
    public bool IsActive { get; set; } = true;
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>OAuth provider (e.g., "Google"). Null for email/password accounts.</summary>
    public string? AuthProvider { get; set; }

    /// <summary>External provider user ID (e.g., Google sub claim).</summary>
    public string? AuthProviderId { get; set; }

    /// <summary>Hashed refresh token for JWT rotation. Null when no active session.</summary>
    public string? RefreshTokenHash { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }

    // Navigation properties
    public ICollection<UserVocabulary> UserVocabularies { get; set; } = [];
    public UserSetting? UserSetting { get; set; }
    public ICollection<ReviewLog> ReviewLogs { get; set; } = [];
}
