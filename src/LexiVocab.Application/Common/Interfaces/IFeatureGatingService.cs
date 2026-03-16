using LexiVocab.Application.DTOs.Auth;

namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// Service responsible for enforcing freemium business rules,
/// such as quota limits and feature gating based on the user's plan.
/// </summary>
public interface IFeatureGatingService
{
    /// <summary>Gets the user's full permission matrix and quotas.</summary>
    Task<UserPermissionsDto> GetPermissionsAsync(Guid userId, CancellationToken ct);

    /// <summary>Checks if a user has access to a specific feature.</summary>
    Task<bool> HasFeatureAsync(Guid userId, string featureCode, CancellationToken ct);

    /// <summary>
    /// Consumes one unit of quota for a specific feature. 
    /// Returns false if quota is exhausted.
    /// </summary>
    Task<bool> ConsumeQuotaAsync(Guid userId, string featureCode, string quotaLimitCode, CancellationToken ct);
}
