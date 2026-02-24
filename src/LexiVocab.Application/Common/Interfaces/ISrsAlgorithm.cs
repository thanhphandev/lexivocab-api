using LexiVocab.Domain.Enums;

namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// SuperMemo-2 algorithm service interface.
/// Pure calculation — no side effects, no database access.
/// </summary>
public interface ISrsAlgorithm
{
    /// <summary>
    /// Calculate the next SRS state after a review attempt.
    /// </summary>
    SrsCalculationResult Calculate(
        int currentRepetitionCount,
        double currentEasinessFactor,
        int currentIntervalDays,
        QualityScore quality);
}

/// <summary>
/// Output of the SM-2 calculation.
/// </summary>
public record SrsCalculationResult(
    int NewRepetitionCount,
    double NewEasinessFactor,
    int NewIntervalDays,
    DateTime NextReviewDate);
