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
        if (await _dbContext.FeatureDefinitions.AnyAsync(cancellationToken))
        {
            return; // Already seeded
        }

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
                DefaultValue = "0",
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
                Code = "EXPORT_PDF",
                Name = "Export to PDF",
                Description = "Ability to export vocabulary lists to PDF",
                ValueType = "boolean",
                DefaultValue = "false",
                CreatedAt = seedDate
            },
            new FeatureDefinition
            {
                Id = new Guid("f5555555-5555-5555-5555-555555555555"),
                Code = "MAX_TAGS",
                Name = "Maximum Tags",
                Description = "Limit of vocabulary tags allowed",
                ValueType = "integer",
                DefaultValue = "5",
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
            }
        };

        await _dbContext.FeatureDefinitions.AddRangeAsync(features, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
