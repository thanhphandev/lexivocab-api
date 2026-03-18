namespace LexiVocab.Domain.Enums;

/// <summary>
/// Represents the billing frequency for a subscription pricing option.
/// </summary>
public enum BillingCycle
{
    Lifetime = -1,
    Free = 0,
    Monthly = 1,
    Quarterly = 3,
    SemiAnnual = 6,
    Annual = 12
}
