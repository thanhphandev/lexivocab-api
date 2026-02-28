namespace LexiVocab.Application.DTOs.Auth;

/// <summary>
/// Data Transfer Object representing the user's current subscription plan and feature permissions.
/// Consumed by the frontend to conditionally render UI.
/// </summary>
public record UserPermissionsDto(
    string Plan,
    int MaxVocabularies,
    int CurrentCount,
    bool CanExportData,
    bool CanUseAi,
    bool CanBatchImport,
    DateTime? PlanExpiresAt
);
