using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Domain.Enums;

namespace LexiVocab.Application.Services;

/// <summary>
/// SuperMemo-2 (SM-2) algorithm implementation.
/// Pure calculation — no side effects, no database calls, fully testable.
///
/// Algorithm reference: https://www.supermemo.com/en/archives1990-2015/english/ol/sm2
///
/// Core formula:
///   EF' = EF + (0.1 - (5 - q) * (0.08 + (5 - q) * 0.02))
///   where q = quality score (0-5), EF = easiness factor (min 1.3)
///
/// Interval calculation:
///   If q &lt; 3 → reset to interval = 1 day (re-learn)
///   If rep == 1 → interval = 1
///   If rep == 2 → interval = 6
///   Else        → interval = previous_interval * EF
/// </summary>
public class SrsAlgorithmService : ISrsAlgorithm
{
    private const double MinEasinessFactor = 1.3;

    public SrsCalculationResult Calculate(
        int currentRepetitionCount,
        double currentEasinessFactor,
        int currentIntervalDays,
        QualityScore quality)
    {
        var q = (int)quality;

        // ─── Calculate new Easiness Factor ────────────────────
        var newEf = currentEasinessFactor + (0.1 - (5 - q) * (0.08 + (5 - q) * 0.02));
        newEf = Math.Max(MinEasinessFactor, newEf);

        int newRepCount;
        int newInterval;

        if (q < 3)
        {
            // Failed recall → reset repetition cycle
            newRepCount = 0;
            newInterval = 1;
        }
        else
        {
            // Successful recall → advance
            newRepCount = currentRepetitionCount + 1;

            newInterval = newRepCount switch
            {
                1 => 1,
                2 => 6,
                _ => (int)Math.Ceiling(currentIntervalDays * newEf)
            };
        }

        var nextReviewDate = DateTime.UtcNow.AddDays(newInterval);

        return new SrsCalculationResult(
            newRepCount,
            Math.Round(newEf, 2),
            newInterval,
            nextReviewDate);
    }
}
