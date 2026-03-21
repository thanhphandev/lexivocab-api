namespace LexiVocab.Application.DTOs.Auth;

/// <summary>
/// Data Transfer Object representing the user's current subscription plan and feature permissions.
/// Consumed by the frontend to conditionally render UI.
/// </summary>
public record UserPermissionsDto(
    string Plan,
    int CurrentCount,
    DateTime? PlanExpiresAt,
    Dictionary<string, string> FeatureFlags,
    Dictionary<string, int> QuotaUsages = null!
)
{
    public UserPermissionsDto() : this(string.Empty, 0, null, new(), new()) { }
    public bool HasFeature(string code) => 
        FeatureFlags.TryGetValue(code, out var val) && val.Equals("true", StringComparison.OrdinalIgnoreCase);

    public int GetLimit(string code, int defaultValue = 0)
    {
        if (FeatureFlags.TryGetValue(code, out var val) && int.TryParse(val, out var limit))
        {
            return limit;
        }
        return defaultValue;
    }

    public bool IsOverQuota(string code, int currentUsage)
    {
        var limit = GetLimit(code);
        if (limit == -1) return false;
        return currentUsage >= limit;
    }
}
