namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// Marker interface to indicate that a MediatR query's response should be cached.
/// Requires an explicit CacheKey and duration.
/// </summary>
/// <typeparam name="TResponse">The return type of the query.</typeparam>
public interface ICacheableQuery<TResponse>
{
    string CacheKey { get; }
    TimeSpan? CacheDuration { get; } // Nullable, if null defaults conceptually to an infrastructure policy or is explicit per query
}
