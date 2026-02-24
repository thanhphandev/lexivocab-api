namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// Abstracts the current authenticated user from HttpContext.
/// Injected as Scoped to resolve per-request.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
}
