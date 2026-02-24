namespace LexiVocab.Domain.Enums;

/// <summary>
/// SM-2 quality rating scale (0-5).
/// Used when user self-evaluates recall during flashcard review.
/// </summary>
public enum QualityScore
{
    /// <summary>Complete blackout — no memory of the word at all.</summary>
    CompleteBlackout = 0,

    /// <summary>Wrong answer, but recognized the word upon seeing the correct answer.</summary>
    IncorrectButRecognized = 1,

    /// <summary>Wrong answer, but the correct answer seemed easy to recall.</summary>
    IncorrectButEasyRecall = 2,

    /// <summary>Correct answer, but required significant effort to recall.</summary>
    CorrectWithDifficulty = 3,

    /// <summary>Correct answer with some hesitation.</summary>
    CorrectWithHesitation = 4,

    /// <summary>Perfect response — instant and confident recall.</summary>
    Perfect = 5
}
