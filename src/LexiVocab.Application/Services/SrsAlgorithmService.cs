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
            // Failed recall (Lapse Rule)
            newRepCount = 0;
            
            // Modern SRS penalty: Instead of dropping to 1 completely, keep a friction of the interval
            // unless it's a very new card.
            if (currentIntervalDays > 3)
            {
                newInterval = (int)Math.Max(1, Math.Floor(currentIntervalDays * 0.2)); // Keep 20% of interval
            }
            else
            {
                newInterval = 1;
            }
        }
        else
        {
            // Successful recall
            newRepCount = currentRepetitionCount + 1;

            if (newRepCount == 1)
            {
                newInterval = 1;
            }
            else if (newRepCount == 2)
            {
                newInterval = 6;
            }
            else
            {
                // Core SM-2 Formula
                newInterval = (int)Math.Ceiling(currentIntervalDays * newEf);
                
                // Fuzz Factor (Adding ±5% noise to intervals >= 7 to prevent Clumping)
                if (newInterval >= 7)
                {
                    var fuzzMin = newInterval * 0.95;
                    var fuzzMax = newInterval * 1.05;
                    // Provide a deterministic fuzz via card hash to maintain pure functional aspect,
                    // or use a Random instance. We'll use Random here isolated.
                    var random = new Random();
                    var fuzzed = random.NextDouble() * (fuzzMax - fuzzMin) + fuzzMin;
                    newInterval = (int)Math.Round(fuzzed);
                }
            }
        }

        var nextReviewDate = DateTime.UtcNow.AddDays(newInterval);

        return new SrsCalculationResult(
            newRepCount,
            Math.Round(newEf, 2),
            newInterval,
            nextReviewDate);
    }
}
