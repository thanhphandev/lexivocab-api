namespace LexiVocab.Application.Common.Interfaces;

public interface IFeatureGatedRequest
{
    string FeatureCode { get; }
    string? QuotaLimitCode { get; }
}
