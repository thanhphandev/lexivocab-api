using LexiVocab.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds PlanDefinition entities with default subscription plans and their features.
/// Requires FeatureDefinitionSeeder to run first.
/// </summary>
public class PlanDefinitionSeeder : IDataSeeder
{
    private readonly AppDbContext _dbContext;

    public int Order => 2; // Run after FeatureSeeder

    public PlanDefinitionSeeder(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _dbContext.PlanDefinitions.AnyAsync(cancellationToken))
        {
            return; // Already seeded
        }

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Feature IDs
        var maxWordsId = new Guid("f1111111-1111-1111-1111-111111111111");
        var aiAccessId = new Guid("f2222222-2222-2222-2222-222222222222");
        var supportLevelId = new Guid("f3333333-3333-3333-3333-333333333333");
        var exportPdfId = new Guid("f4444444-4444-4444-4444-444444444444");
        var batchImportId = new Guid("f6666666-6666-6666-6666-666666666666");
        var aiDailyLimitId = new Guid("f7777777-7777-7777-7777-777777777777");
        var maxQuizPerDayId = new Guid("f8888888-8888-8888-8888-888888888888");
        var advancedAiId = new Guid("f9999999-9999-9999-9999-999999999999");
        var quizGenerationId = new Guid("faaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Free Plan
        var freePlan = new PlanDefinition
        {
            Id = new Guid("11111111-1111-1111-1111-111111111111"),
            Name = "Free",
            NameKey = "plan_free",
            Price = 0m,
            Currency = "VND",
            Description = "Perfect for beginners to get started",
            IntervalType = "Lifetime",
            DurationDays = 0,
            IsActive = true,
            IsRecommended = false,
            CreatedAt = seedDate,
            PlanFeatures =
            [
                new PlanFeature { FeatureDefinitionId = maxWordsId, Value = "50" },
                new PlanFeature { FeatureDefinitionId = aiAccessId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = supportLevelId, Value = "Community" },
                new PlanFeature { FeatureDefinitionId = exportPdfId, Value = "false" },
                new PlanFeature { FeatureDefinitionId = batchImportId, Value = "false" },
                new PlanFeature { FeatureDefinitionId = aiDailyLimitId, Value = "10" },
                new PlanFeature { FeatureDefinitionId = maxQuizPerDayId, Value = "5" },
                new PlanFeature { FeatureDefinitionId = advancedAiId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = quizGenerationId, Value = "true" }
            ]
        };

        // Premium Plan (Base - Price per month, interval flexible from client)
        var premiumPlan = new PlanDefinition
        {
            Id = new Guid("22222222-2222-2222-2222-222222222222"),
            Name = "Premium",
            NameKey = "plan_premium",
            Price = 5000m, // Base price per month
            Currency = "VND",
            Description = "Unlock full potential with AI features",
            IntervalType = "Month", // Client can request 1, 3, 6, 12 months
            DurationDays = 30, // Minimum 1 month
            IsActive = true,
            IsRecommended = true,
            CreatedAt = seedDate,
            PlanFeatures =
            [
                new PlanFeature { FeatureDefinitionId = maxWordsId, Value = "1000" },
                new PlanFeature { FeatureDefinitionId = aiAccessId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = supportLevelId, Value = "Email" },
                new PlanFeature { FeatureDefinitionId = exportPdfId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = batchImportId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = aiDailyLimitId, Value = "50" },
                new PlanFeature { FeatureDefinitionId = maxQuizPerDayId, Value = "20" },
                new PlanFeature { FeatureDefinitionId = advancedAiId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = quizGenerationId, Value = "true" }
            ]
        };

        // Business Plan (Lifetime)
        var businessPlan = new PlanDefinition
        {
            Id = new Guid("44444444-4444-4444-4444-444444444444"),
            Name = "Business",
            NameKey = "plan_business",
            Price = 4990000m,
            Currency = "VND",
            Description = "Lifetime access for professionals and teams",
            IntervalType = "Lifetime",
            DurationDays = 0,
            IsActive = true,
            IsRecommended = false,
            CreatedAt = seedDate,
            PlanFeatures =
            [
                new PlanFeature { FeatureDefinitionId = maxWordsId, Value = "Unlimited" },
                new PlanFeature { FeatureDefinitionId = aiAccessId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = supportLevelId, Value = "24/7 Priority" },
                new PlanFeature { FeatureDefinitionId = exportPdfId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = batchImportId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = aiDailyLimitId, Value = "Unlimited" },
                new PlanFeature { FeatureDefinitionId = maxQuizPerDayId, Value = "Unlimited" },
                new PlanFeature { FeatureDefinitionId = advancedAiId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = quizGenerationId, Value = "true" }
            ]
        };

        await _dbContext.PlanDefinitions.AddRangeAsync(
            [freePlan, premiumPlan, businessPlan], cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
