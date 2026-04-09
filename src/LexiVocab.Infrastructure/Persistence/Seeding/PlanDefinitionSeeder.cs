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
        var availableModelsId = new Guid("fccccccc-cccc-cccc-cccc-cccccccccccc");
        var allModelsJson = "[{\"id\":\"gh/gpt-4o-mini\", \"name\":\"GPT-4o Mini\", \"provider\":\"custom\", \"group\":\"Free Models\"}, {\"id\":\"gh/gpt-4o\", \"name\":\"GPT-4o (Standard)\", \"provider\":\"custom\", \"group\":\"Free Models\"}, {\"id\":\"groq/moonshotai/kimi-k2-instruct-0905\", \"name\":\"Kimi K2 Instruct\", \"provider\":\"custom\", \"group\":\"Free Models\"}, {\"id\":\"gh/grok-code-fast-1\", \"name\":\"Grok Code Fast\", \"provider\":\"custom\", \"group\":\"Free Models\"}, {\"id\":\"groq/openai/gpt-oss-120b\", \"name\":\"GPT OSS 120b\", \"provider\":\"custom\", \"group\":\"Free Models\"}, {\"id\":\"cx/gpt-5.4\", \"name\":\"GPT-5.4 Ultra\", \"provider\":\"custom\", \"group\":\"Advanced Models\", \"isPro\":true}, {\"id\":\"cx/gpt-5-codex-mini\", \"name\":\"GPT-5 Codex Mini\", \"provider\":\"custom\", \"group\":\"Advanced Models\", \"isPro\":true}, {\"id\":\"gh/claude-opus-4.6\", \"name\":\"Claude 4.6 Opus\", \"provider\":\"custom\", \"group\":\"Advanced Models\", \"isPro\":true}, {\"id\":\"gh/gpt-5.1\", \"name\":\"GPT-5.1 Pro\", \"provider\":\"custom\", \"group\":\"Advanced Models\", \"isPro\":true}, {\"id\":\"qw/qwen3-coder-plus\", \"name\":\"Qwen3 Coder Plus\", \"provider\":\"custom\", \"group\":\"Advanced Models\", \"isPro\":true}, {\"id\":\"qw/qwen3-coder-flash\", \"name\":\"Qwen3 Coder Flash\", \"provider\":\"custom\", \"group\":\"Advanced Models\", \"isPro\":true}, {\"id\":\"gh/gemini-3-pro-preview\", \"name\":\"Gemini 3 Pro Preview\", \"provider\":\"custom\", \"group\":\"Advanced Models\", \"isPro\":true}, {\"id\":\"gh/claude-sonnet-4.6\", \"name\":\"Claude Sonnet 4.6\", \"provider\":\"custom\", \"group\":\"Advanced Models\", \"isPro\":true}]";

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Feature IDs
        var maxWordsId = new Guid("f1111111-1111-1111-1111-111111111111");
        var aiAccessId = new Guid("f2222222-2222-2222-2222-222222222222");
        var supportLevelId = new Guid("f3333333-3333-3333-3333-333333333333");
        var exportAnkiId = new Guid("f4444444-4444-4444-4444-444444444444");
        var batchImportId = new Guid("f6666666-6666-6666-6666-666666666666");
        var aiDailyLimitId = new Guid("f7777777-7777-7777-7777-777777777777");
        var maxQuizPerDayId = new Guid("f8888888-8888-8888-8888-888888888888");
        var advancedAiId = new Guid("f9999999-9999-9999-9999-999999999999");
        var quizGenerationId = new Guid("faaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var llmTranslationLimitId = new Guid("fbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // We define the plans first, then check if we need to insert or sync later.

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
                new PlanFeature { FeatureDefinitionId = maxWordsId, Value = "30" },
                new PlanFeature { FeatureDefinitionId = aiAccessId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = supportLevelId, Value = "Community" },
                new PlanFeature { FeatureDefinitionId = exportAnkiId, Value = "false" },
                new PlanFeature { FeatureDefinitionId = batchImportId, Value = "false" },
                new PlanFeature { FeatureDefinitionId = aiDailyLimitId, Value = "10" },
                new PlanFeature { FeatureDefinitionId = maxQuizPerDayId, Value = "5" },
                new PlanFeature { FeatureDefinitionId = advancedAiId, Value = "false" },
                new PlanFeature { FeatureDefinitionId = quizGenerationId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = availableModelsId, Value = allModelsJson },
                new PlanFeature { FeatureDefinitionId = llmTranslationLimitId, Value = "20" }
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
                    Price = 49000m, 
                    Currency = "VND",
                    DurationDays = 30,
                    LabelKey = "duration_1m",
                    SortOrder = 1,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("e2222222-2222-2222-2222-333333333333"),
                    BillingCycle = BillingCycle.SemiAnnual,
                    Price = 249000m,
                    Currency = "VND",
                    DurationDays = 180,
                    LabelKey = "duration_6m",
                    SortOrder = 2,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("e2222222-2222-2222-2222-444444444444"),
                    BillingCycle = BillingCycle.Annual,
                    Price = 469000m,
                    Currency = "VND",
                    DurationDays = 365,
                    LabelKey = "duration_12m",
                    SortOrder = 3,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("c2222222-2222-2222-2222-111111111111"),
                    BillingCycle = BillingCycle.Monthly,
                    Price = 1.99m, 
                    Currency = "USD",
                    DurationDays = 30,
                    LabelKey = "duration_1m",
                    SortOrder = 4,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("c2222222-2222-2222-2222-333333333333"),
                    BillingCycle = BillingCycle.SemiAnnual,
                    Price = 9.99m,
                    Currency = "USD",
                    DurationDays = 180,
                    LabelKey = "duration_6m",
                    SortOrder = 5,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("c2222222-2222-2222-2222-444444444444"),
                    BillingCycle = BillingCycle.Annual,
                    Price = 19.99m,
                    Currency = "USD",
                    DurationDays = 365,
                    LabelKey = "duration_12m",
                    SortOrder = 6,
                    IsActive = true,
                    CreatedAt = seedDate
                }
            ],
            PlanFeatures =
            [
                new PlanFeature { FeatureDefinitionId = maxWordsId, Value = "2000" },
                new PlanFeature { FeatureDefinitionId = aiAccessId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = supportLevelId, Value = "Email" },
                new PlanFeature { FeatureDefinitionId = exportAnkiId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = batchImportId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = aiDailyLimitId, Value = "100" },
                new PlanFeature { FeatureDefinitionId = maxQuizPerDayId, Value = "25" },
                new PlanFeature { FeatureDefinitionId = advancedAiId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = quizGenerationId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = availableModelsId, Value = allModelsJson },
                new PlanFeature { FeatureDefinitionId = llmTranslationLimitId, Value = "500" }
            ]
        };

        // Ultimate Plan
        var ultimatePlan = new PlanDefinition
        {
            Id = new Guid("44444444-4444-4444-4444-444444444444"),
            Name = "Ultimate",
            NameKey = "plan_ultimate",
            Description = "Unlimited access for professionals and teams",
            DisplayOrder = 3,
            IsActive = true,
            IsRecommended = false,
            CreatedAt = seedDate,
            Pricings =
            [
                new PlanPricing {
                    Id = new Guid("e4444444-4444-4444-4444-111111111111"),
                    BillingCycle = BillingCycle.Monthly,
                    Price = 99000m,
                    Currency = "VND",
                    DurationDays = 30,
                    LabelKey = "duration_1m",
                    SortOrder = 1,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("e4444444-4444-4444-4444-222222222222"),
                    BillingCycle = BillingCycle.Annual,
                    Price = 890000m,
                    Currency = "VND",
                    DurationDays = 365,
                    LabelKey = "duration_12m",
                    SortOrder = 2,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("e4444444-4444-4444-4444-333333333333"),
                    BillingCycle = BillingCycle.Lifetime,
                    Price = 2490000m,
                    Currency = "VND",
                    DurationDays = null,
                    LabelKey = "duration_lifetime",
                    SortOrder = 3,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("c4444444-4444-4444-4444-111111111111"),
                    BillingCycle = BillingCycle.Monthly,
                    Price = 4.99m,
                    Currency = "USD",
                    DurationDays = 30,
                    LabelKey = "duration_1m",
                    SortOrder = 4,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("c4444444-4444-4444-4444-222222222222"),
                    BillingCycle = BillingCycle.Annual,
                    Price = 39.99m,
                    Currency = "USD",
                    DurationDays = 365,
                    LabelKey = "duration_12m",
                    SortOrder = 5,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new PlanPricing {
                    Id = new Guid("c4444444-4444-4444-4444-333333333333"),
                    BillingCycle = BillingCycle.Lifetime,
                    Price = 99.99m,
                    Currency = "USD",
                    DurationDays = null,
                    LabelKey = "duration_lifetime",
                    SortOrder = 6,
                    IsActive = true,
                    CreatedAt = seedDate
                }
            ],
            PlanFeatures =
            [
                new PlanFeature { FeatureDefinitionId = maxWordsId, Value = "-1" },
                new PlanFeature { FeatureDefinitionId = aiAccessId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = supportLevelId, Value = "24/7 Priority" },
                new PlanFeature { FeatureDefinitionId = exportAnkiId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = batchImportId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = aiDailyLimitId, Value = "-1" },
                new PlanFeature { FeatureDefinitionId = maxQuizPerDayId, Value = "-1" },
                new PlanFeature { FeatureDefinitionId = advancedAiId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = quizGenerationId, Value = "true" },
                new PlanFeature { FeatureDefinitionId = availableModelsId, Value = allModelsJson },
                new PlanFeature { FeatureDefinitionId = llmTranslationLimitId, Value = "-1" }
            ]
        };

        var existingPlans = await _dbContext.PlanDefinitions
            .Include(p => p.PlanFeatures)
            .ToListAsync(cancellationToken);

        var definedPlans = new[] { freePlan, premiumPlan, ultimatePlan };

        if (!existingPlans.Any())
        {
            await _dbContext.PlanDefinitions.AddRangeAsync(definedPlans, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        bool hasChanges = false;
        foreach (var existingPlan in existingPlans)
        {
            var definedPlan = definedPlans.FirstOrDefault(p => p.Id == existingPlan.Id);
            if (definedPlan != null)
            {
                // Sync missing PlanPricing
                foreach (var definedPrice in definedPlan.Pricings)
                {
                    if (!existingPlan.Pricings.Any(p => p.Id == definedPrice.Id))
                    {
                        existingPlan.Pricings.Add(definedPrice);
                        hasChanges = true;
                    }
                }

                // Sync missing PlanFeatures
                foreach (var definedFeature in definedPlan.PlanFeatures)
                {
                    var existingFeature = existingPlan.PlanFeatures
                        .FirstOrDefault(f => f.FeatureDefinitionId == definedFeature.FeatureDefinitionId);

                    if (existingFeature == null)
                    {
                        existingPlan.PlanFeatures.Add(new PlanFeature
                        {
                            PlanDefinitionId = existingPlan.Id,
                            FeatureDefinitionId = definedFeature.FeatureDefinitionId,
                            Value = definedFeature.Value
                        });
                        hasChanges = true;
                    }
                    else if (existingFeature.Value != definedFeature.Value)
                    {
                        existingFeature.Value = definedFeature.Value;
                        hasChanges = true;
                    }
                }
            }
        }

        if (hasChanges)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
