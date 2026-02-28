using LexiVocab.Domain.Enums;

namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// Marker interface for MediatR commands/queries that should be automatically audited.
/// Implement this on any IRequest to have the <see cref="Behaviors.AuditLoggingBehavior{TRequest,TResponse}"/>
/// capture the action in the AuditLogs table.
///
/// Usage example:
/// <code>
/// public record CreateVocabCommand(...) : IRequest&lt;Result&lt;VocabDto&gt;&gt;, IAuditedRequest
/// {
///     public AuditAction AuditAction => AuditAction.VocabularyCreated;
///     public string? EntityType => "UserVocabulary";
///     public string? EntityId => null; // Set after creation
///     public string? AdditionalInfo => null;
/// }
/// </code>
/// </summary>
public interface IAuditedRequest
{
    /// <summary>The type of action being performed.</summary>
    AuditAction AuditAction { get; }

    /// <summary>The entity type being affected (optional, for targeted auditing).</summary>
    string? EntityType => null;

    /// <summary>The entity ID being affected (if known at request time).</summary>
    string? EntityId => null;

    /// <summary>Any additional context to include in the audit record.</summary>
    string? AdditionalInfo => null;
}
