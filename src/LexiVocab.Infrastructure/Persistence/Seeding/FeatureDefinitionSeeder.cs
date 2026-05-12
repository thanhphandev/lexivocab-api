using LexiVocab.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds FeatureDefinition entities with default system features.
/// </summary>
public class FeatureDefinitionSeeder : IDataSeeder
{
    private readonly AppDbContext _dbContext;

    public int Order => 1; // Run before PlanSeeder

    public FeatureDefinitionSeeder(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var features = new[]
        {
            new FeatureDefinition
            {
                Id = new Guid("f1111111-1111-1111-1111-111111111111"),
                Code = "MAX_WORDS",
                Name = "Maximum Words",
                Description = "Limit of vocabulary words saved",
                ValueType = "integer",
                DefaultValue = "30",
                CreatedAt = seedDate
            },
            new FeatureDefinition
            {
                Id = new Guid("f2222222-2222-2222-2222-222222222222"),
                Code = "AI_ACCESS",
                Name = "AI Features",
                Description = "Access to AI analysis and generation",
                ValueType = "boolean",
                DefaultValue = "false",
                CreatedAt = seedDate
            },
            new FeatureDefinition
            {
                Id = new Guid("f3333333-3333-3333-3333-333333333333"),
                Code = "SUPPORT_LEVEL",
                Name = "Support Level",
                Description = "Customer support priority level",
                ValueType = "string",
                DefaultValue = "Community",
                CreatedAt = seedDate
            },
            new FeatureDefinition
            {
                Id = new Guid("f4444444-4444-4444-4444-444444444444"),
                Code = "EXPORT_ANKI",
                Name = "Export Anki/PDF",
                Description = "Ability to export vocabulary lists to Anki and PDF",
                ValueType = "boolean",
                DefaultValue = "false",
                CreatedAt = seedDate
            },
            new FeatureDefinition
            {
                Id = new Guid("f6666666-6666-6666-6666-666666666666"),
                Code = "BATCH_IMPORT",
                Name = "Batch Import",
                Description = "Ability to import multiple words at once",
                ValueType = "boolean",
                DefaultValue = "false",
                CreatedAt = seedDate
            },
            new FeatureDefinition
            {
                Id = new Guid("f7777777-7777-7777-7777-777777777777"),
                Code = "AI_DAILY_LIMIT",
                Name = "AI Daily Limit",
                Description = "Maximum AI requests per day",
                ValueType = "integer",
                DefaultValue = "10",
                CreatedAt = seedDate
            },
            new FeatureDefinition
            {
                Id = new Guid("f8888888-8888-8888-8888-888888888888"),
                Code = "MAX_QUIZ_PER_DAY",
                Name = "Quiz Daily Limit",
                Description = "Maximum quiz generation requests per day",
                ValueType = "integer",
                DefaultValue = "5",
                CreatedAt = seedDate
            },
            new FeatureDefinition
            {
                Id = new Guid("f9999999-9999-9999-9999-999999999999"),
                Code = "ADVANCED_AI",
                Name = "Advanced AI Features",
                Description = "Access to advanced AI analysis and generation",
                ValueType = "boolean",
                DefaultValue = "false",
                CreatedAt = seedDate
            },
            new FeatureDefinition
            {
                Id = new Guid("faaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Code = "QUIZ_GENERATION",
                Name = "Quiz Generation",
                Description = "Ability to generate AI-powered quizzes",
                ValueType = "boolean",
                DefaultValue = "false",
                CreatedAt = seedDate
            },
            new FeatureDefinition
            {
                Id = new Guid("fbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Code = "LLM_TRANSLATION_LIMIT",
                Name = "LLM Translation Limit",
                Description = "Maximum LLM translation requests per day",
                ValueType = "integer",
                DefaultValue = "20",
                CreatedAt = seedDate
            },
            new FeatureDefinition
            {
                Id = new Guid("fccccccc-cccc-cccc-cccc-cccccccccccc"),
                Code = "AVAILABLE_LLM_MODELS",
                Name = "Available LLM Models",
                Description = "JSON array of allowed models",
                ValueType = "json",
                DefaultValue = "[{\"id\":\"llama-3.1-8b-instant\", \"name\":\"Llama 3.1 8B (Fast)\", \"provider\":\"custom\", \"group\":\"Free Models\"}, {\"id\":\"llama-3.3-70b-versatile\", \"name\":\"Llama 3.3 70B (Versatile)\", \"provider\":\"custom\", \"group\":\"Free Models\"}, {\"id\":\"openai/gpt-oss-20b\", \"name\":\"GPT-OSS 20B\", \"provider\":\"custom\", \"group\":\"Free Models\"}, {\"id\":\"qwen/qwen3-32b\", \"name\":\"Qwen3 32B (Preview)\", \"provider\":\"custom\", \"group\":\"Free Models\"}, {\"id\":\"openai/gpt-oss-120b\", \"name\":\"GPT-OSS 120B Pro\", \"provider\":\"custom\", \"group\":\"Advanced Models\", \"isPro\":true}]",
                CreatedAt = seedDate
            }
        };

        var existingFeatures = await _dbContext.FeatureDefinitions
            .ToListAsync(cancellationToken);
        var existingCodes = existingFeatures.Select(f => f.Code).ToList();

        var newFeatures = features.Where(f => !existingCodes.Contains(f.Code)).ToList();
        
        if (newFeatures.Any())
        {
            await _dbContext.FeatureDefinitions.AddRangeAsync(newFeatures, cancellationToken);
        }

        // Patch existing AVAILABLE_LLM_MODELS just in case it exists but with old values
        var availableFeature = existingFeatures.FirstOrDefault(f => f.Code == "AVAILABLE_LLM_MODELS");
        var featureToPatch = features.First(f => f.Code == "AVAILABLE_LLM_MODELS");
        if (availableFeature != null && availableFeature.DefaultValue != featureToPatch.DefaultValue)
        {
            availableFeature.DefaultValue = featureToPatch.DefaultValue;
            
            var planFeaturesToUpdate = await _dbContext.Set<PlanFeature>()
                .Where(pf => pf.FeatureDefinitionId == availableFeature.Id)
                .ToListAsync(cancellationToken);
                
            foreach (var pf in planFeaturesToUpdate)
            {
                pf.Value = featureToPatch.DefaultValue;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
