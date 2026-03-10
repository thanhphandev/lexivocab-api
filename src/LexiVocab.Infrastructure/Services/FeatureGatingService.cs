using LexiVocab.Application.Common.Interfaces;
using LexiVocab.Application.DTOs.Auth;
using LexiVocab.Domain.Enums;
using LexiVocab.Domain.Interfaces;

namespace LexiVocab.Infrastructure.Services;

public class FeatureGatingService : IFeatureGatingService
{
    private readonly IUnitOfWork _uow;

    public FeatureGatingService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> IsPremiumAsync(Guid userId, CancellationToken ct)
    {
        // Check for ANY active subscription that is not the "Free" plan
        var activeSub = await _uow.Subscriptions.GetActiveByUserIdAsync(userId, ct);

        return activeSub != null && activeSub.PlanDefinition.Name != "Free";
    }

    public async Task<UserPermissionsDto> GetPermissionsAsync(Guid userId, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);

        var currentCount = await _uow.Vocabularies
            .CountByUserIdAsync(userId, ct);

        // Fetch active subscription with its plan and features
        var activeSub = await _uow.Subscriptions.GetActiveWithFeaturesAsync(userId, ct);

        // If no active sub (or expired), fall back to Free tier
        if (activeSub == null || (activeSub.EndDate.HasValue && activeSub.EndDate.Value < DateTime.UtcNow))
        {
            var freePlan = await GetPlanByCodeAsync("Free", ct);
            return CreatePermissionsDto(freePlan, currentCount, null);
        }

        return CreatePermissionsDto(activeSub.PlanDefinition, currentCount, activeSub.EndDate);
    }

    public async Task<bool> CanCreateVocabularyAsync(Guid userId, CancellationToken ct)
    {
         var permissions = await GetPermissionsAsync(userId, ct);
         if (permissions.MaxVocabularies >= 999999) return true;
         return permissions.CurrentCount < permissions.MaxVocabularies;
    }

    private async Task<Domain.Entities.PlanDefinition?> GetPlanByCodeAsync(string name, CancellationToken ct)
    {
        return await _uow.PlanDefinitions.GetByNameWithFeaturesAsync(name, ct);
    }

    private static UserPermissionsDto CreatePermissionsDto(Domain.Entities.PlanDefinition? plan, int currentCount, DateTime? expiration)
    {
        if (plan == null) return new UserPermissionsDto("None", 0, currentCount, false, false, false, expiration);

        var maxWordsValue = plan.PlanFeatures.FirstOrDefault(pf => pf.Feature.Code == "MAX_WORDS")?.Value ?? "50";
        var maxVocab = maxWordsValue.Equals("Unlimited", StringComparison.OrdinalIgnoreCase) ? 999999 : int.Parse(maxWordsValue);

        return new UserPermissionsDto(
            Plan: plan.Name,
            MaxVocabularies: maxVocab,
            CurrentCount: currentCount,
            CanExportData: plan.PlanFeatures.FirstOrDefault(pf => pf.Feature.Code == "EXPORT_PDF")?.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false,
            CanUseAi: plan.PlanFeatures.FirstOrDefault(pf => pf.Feature.Code == "AI_ACCESS")?.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false,
            CanBatchImport: plan.PlanFeatures.FirstOrDefault(pf => pf.Feature.Code == "BATCH_IMPORT")?.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false, // Mapped from your tier requirements
            PlanExpiresAt: expiration);
    }

}
