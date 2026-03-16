using LexiVocab.Application.Common.Interfaces;

namespace LexiVocab.Infrastructure.Services;

/// <summary>
/// Implementation of dynamic pricing with volume discounts
/// Discount tiers: 1 month (0%), 3 months (5%), 6 months (10%), 12 months (20%)
/// </summary>
public class PricingCalculator : IPricingCalculator
{
    // Discount tiers: months -> discount percentage
    private static readonly Dictionary<int, decimal> DiscountTiers = new()
    {
        { 1, 0m },      // 1 month: no discount
        { 3, 0.05m },   // 3 months: 5% off
        { 6, 0.10m },   // 6 months: 10% off
        { 12, 0.20m }   // 12 months: 20% off (best value)
    };

    public PricingResult CalculatePrice(decimal basePrice, int durationMonths)
    {
        // Validate duration
        if (!DiscountTiers.ContainsKey(durationMonths))
        {
            // Default to 1 month if invalid
            durationMonths = 1;
        }

        var discountPercent = DiscountTiers[durationMonths];
        var totalBasePrice = basePrice * durationMonths;
        var discountAmount = totalBasePrice * discountPercent;
        var finalPrice = totalBasePrice - discountAmount;
        var monthlyEquivalent = finalPrice / durationMonths;

        return new PricingResult(
            BasePrice: basePrice,
            DurationMonths: durationMonths,
            DiscountPercent: discountPercent * 100, // Convert to percentage for display
            DiscountAmount: discountAmount,
            FinalPrice: finalPrice,
            MonthlyEquivalent: monthlyEquivalent
        );
    }

    public List<DurationOption> GetDurationOptions()
    {
        return DiscountTiers
            .OrderBy(x => x.Key)
            .Select(x => new DurationOption(
                Months: x.Key,
                DiscountPercent: x.Value * 100,
                LabelKey: $"pricing_duration_{x.Key}m"
            ))
            .ToList();
    }
}
