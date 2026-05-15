namespace LexiVocab.Application.Common.Extensions;

/// <summary>
/// Convenience extensions for ICurrentUserService.
/// Provides a safe, fail-fast alternative to the null-forgiving operator (!).
/// </summary>
public static class CurrentUserServiceExtensions
{
    /// <summary>
    /// Returns the authenticated user's ID or throws UnauthorizedAccessException.
    /// Use instead of <c>UserId!.Value</c> to get a clear exception when unauthenticated.
    /// </summary>
    public static Guid GetRequiredUserId(this Interfaces.ICurrentUserService currentUser)
        => currentUser.UserId
           ?? throw new UnauthorizedAccessException("Authentication required. No valid user context found.");
}
