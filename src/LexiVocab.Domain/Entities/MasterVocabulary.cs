using LexiVocab.Domain.Common;

namespace LexiVocab.Domain.Entities;

/// <summary>
/// Master dictionary of English words. Shared across all users.
/// Prevents redundant storage of phonetics/audio for the same word across millions of user records.
/// Indexed by Word (unique) for O(log n) lookups.
/// </summary>
public class MasterVocabulary : BaseEntity
{
    /// <summary>English word (e.g., "ubiquitous"). Unique, B-Tree indexed.</summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>Part of speech: noun, verb, adjective, etc.</summary>
    public string? PartOfSpeech { get; set; }

    /// <summary>IPA phonetic transcription — UK variant (e.g., /juːˈbɪk.wɪ.təs/).</summary>
    public string? PhoneticUk { get; set; }

    /// <summary>IPA phonetic transcription — US variant.</summary>
    public string? PhoneticUs { get; set; }

    /// <summary>URL to pronunciation audio file (AWS S3 or CDN).</summary>
    public string? AudioUrl { get; set; }

    /// <summary>Popularity rank (e.g., Oxford 3000). Lower = more common.</summary>
    public int? PopularityRank { get; set; }

    // Navigation
    public ICollection<UserVocabulary> UserVocabularies { get; set; } = [];
}
