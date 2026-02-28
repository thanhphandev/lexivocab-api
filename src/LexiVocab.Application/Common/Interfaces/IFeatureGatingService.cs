using LexiVocab.Application.DTOs.Auth;

namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// Service responsible for enforcing freemium business rules,
/// such as quota limits and feature gating based on the user's plan.
/// </summary>
public interface IFeatureGatingService
{
    /// <summary>Checks if a user has reached their vocabulary limit.</summary>
    Task<bool> CanCreateVocabularyAsync(Guid userId, CancellationToken ct);

    /// <summary>Gets the user's full permission matrix and quotas.</summary>
    Task<UserPermissionsDto> GetPermissionsAsync(Guid userId, CancellationToken ct);

    /// <summary>Quick check if a user is currently premium.</summary>
    Task<bool> IsPremiumAsync(Guid userId, CancellationToken ct);
}
