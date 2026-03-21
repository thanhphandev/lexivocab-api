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
                DefaultValue = "[{\"id\":\"cloudflare/@cf/meta/llama-3.1-70b-instruct\", \"name\":\"Llama 3.1 70B\", \"provider\":\"cloudflare\", \"group\":\"Free Models\"}, {\"id\":\"siliconflow/deepseek-r1\", \"name\":\"SiliconFlow DeepSeek\", \"provider\":\"siliconflow\", \"group\":\"Free Models\"}, {\"id\":\"zhipu/glm-4-flash\", \"name\":\"GLM-4 Flash\", \"provider\":\"zhipu\", \"group\":\"Free Models\", \"isWarning\":true}, {\"id\":\"anthropic/claude-4.5-haiku\", \"name\":\"Claude 4.5 Haiku\", \"provider\":\"anthropic\", \"group\":\"Advanced Models\", \"isPro\":true}, {\"id\":\"google/gemini-3-flash\", \"name\":\"Gemini 3 Flash\", \"provider\":\"google\", \"group\":\"Advanced Models\", \"isPro\":true}, {\"id\":\"openai/gpt-5.4\", \"name\":\"GPT 5.4\", \"provider\":\"openai\", \"group\":\"Advanced Models\", \"isPro\":true}, {\"id\":\"openai/gpt-5-mini\", \"name\":\"GPT 5 mini\", \"provider\":\"openai\", \"group\":\"Advanced Models\", \"isPro\":true}, {\"id\":\"qwen/qwen-3.5-plus\", \"name\":\"Qwen 3.5 Plus\", \"provider\":\"qwen\", \"group\":\"Advanced Models\", \"isPro\":true}, {\"id\":\"minmax/minmax-abab6.5s\", \"name\":\"MinMax 6.5s\", \"provider\":\"minmax\", \"group\":\"Advanced Models\", \"isPro\":true}]",
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
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
