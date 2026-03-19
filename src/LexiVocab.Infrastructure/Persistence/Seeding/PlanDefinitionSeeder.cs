using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds PlanDefinition entities with default subscription plans and their features, as well as pricing details.
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
            Description = "Perfect for beginners to get started",
            DisplayOrder = 1,
            IsActive = true,
            IsRecommended = false,
            CreatedAt = seedDate,
            Pricings =
            [
                new PlanPricing {
                    Id = new Guid("e1111111-1111-1111-1111-111111111111"),
                    BillingCycle = BillingCycle.Free,
                    Price = 0m,
                    Currency = "VND",
                    DurationDays = null,
                    LabelKey = "",
                    SortOrder = 1,
                    IsActive = true,
                    CreatedAt = seedDate
                }
            ],
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

        // Premium Plan
        var premiumPlan = new PlanDefinition
        {
            Id = new Guid("22222222-2222-2222-2222-222222222222"),
            Name = "Premium",
            NameKey = "plan_premium",
            Description = "Unlock full potential with AI features",
            DisplayOrder = 2,
            IsActive = true,
            IsRecommended = true,
            CreatedAt = seedDate,
            Pricings =
            [
                new PlanPricing {
                    Id = new Guid("e2222222-2222-2222-2222-111111111111"),
                    BillingCycle = BillingCycle.Monthly,
                    Price = 50000m, 
                    Currency = "VND",
                    DurationDays = 30,
                    LabelKey = "duration_1m",
                    SortOrder = 1,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("e2222222-2222-2222-2222-222222222222"),
                    BillingCycle = BillingCycle.Quarterly,
                    Price = 142500m, // 5% off (base 50k)
                    Currency = "VND",
                    DurationDays = 90,
                    LabelKey = "duration_3m",
                    SortOrder = 2,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("e2222222-2222-2222-2222-333333333333"),
                    BillingCycle = BillingCycle.SemiAnnual,
                    Price = 270000m, // 10% off (base 50k)
                    Currency = "VND",
                    DurationDays = 180,
                    LabelKey = "duration_6m",
                    SortOrder = 3,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("e2222222-2222-2222-2222-444444444444"),
                    BillingCycle = BillingCycle.Annual,
                    Price = 480000m, // 20% off (base 50k)
                    Currency = "VND",
                    DurationDays = 365,
                    LabelKey = "duration_12m",
                    SortOrder = 4,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                // USD TRADES
                new PlanPricing {
                    Id = new Guid("c2222222-2222-2222-2222-111111111111"),
                    BillingCycle = BillingCycle.Monthly,
                    Price = 9.99m, 
                    Currency = "USD",
                    DurationDays = 30,
                    LabelKey = "duration_1m",
                    SortOrder = 5,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("c2222222-2222-2222-2222-222222222222"),
                    BillingCycle = BillingCycle.Quarterly,
                    Price = 27.99m,
                    Currency = "USD",
                    DurationDays = 90,
                    LabelKey = "duration_3m",
                    SortOrder = 6,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("c2222222-2222-2222-2222-333333333333"),
                    BillingCycle = BillingCycle.SemiAnnual,
                    Price = 49.99m,
                    Currency = "USD",
                    DurationDays = 180,
                    LabelKey = "duration_6m",
                    SortOrder = 7,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("c2222222-2222-2222-2222-444444444444"),
                    BillingCycle = BillingCycle.Annual,
                    Price = 89.99m,
                    Currency = "USD",
                    DurationDays = 365,
                    LabelKey = "duration_12m",
                    SortOrder = 8,
                    IsActive = true,
                    CreatedAt = seedDate
                }
            ],
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

        // Business Plan
        var businessPlan = new PlanDefinition
        {
            Id = new Guid("44444444-4444-4444-4444-444444444444"),
            Name = "Business",
            NameKey = "plan_business",
            Description = "Lifetime access for professionals and teams",
            DisplayOrder = 3,
            IsActive = true,
            IsRecommended = false,
            CreatedAt = seedDate,
            Pricings =
            [
                new PlanPricing {
                    Id = new Guid("e4444444-4444-4444-4444-111111111111"),
                    BillingCycle = BillingCycle.Lifetime,
                    Price = 4990000m,
                    Currency = "VND",
                    DurationDays = null,
                    LabelKey = "duration_lifetime",
                    SortOrder = 1,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("c4444444-4444-4444-4444-111111111111"),
                    BillingCycle = BillingCycle.Lifetime,
                    Price = 199.99m,
                    Currency = "USD",
                    DurationDays = null,
                    LabelKey = "duration_lifetime",
                    SortOrder = 2,
                    IsActive = true,
                    CreatedAt = seedDate
                }
            ],
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
