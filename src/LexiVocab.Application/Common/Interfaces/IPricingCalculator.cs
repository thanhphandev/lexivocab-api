namespace LexiVocab.Application.Common.Interfaces;

/// <summary>
/// Service for calculating dynamic pricing with volume discounts
/// </summary>
public interface IPricingCalculator
{
    /// <summary>
    /// Calculate the final price for a plan based on duration with discount
    /// </summary>
    /// <param name="basePrice">Base monthly price from PlanDefinition</param>
    /// <param name="durationMonths">Number of months (1, 3, 6, 12)</param>
    /// <returns>PricingResult with breakdown</returns>
    PricingResult CalculatePrice(decimal basePrice, int durationMonths);
    
    /// <summary>
    /// Get available duration options with their discount rates
    /// </summary>
    List<DurationOption> GetDurationOptions();
}

public record PricingResult(
    decimal BasePrice,
    int DurationMonths,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal FinalPrice,
    decimal MonthlyEquivalent // Price per month after discount
);

public record DurationOption(
    int Months,
    decimal DiscountPercent,
    string LabelKey // i18n key for label
);
