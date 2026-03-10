using LexiVocab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class FeatureDefinitionConfiguration : IEntityTypeConfiguration<FeatureDefinition>
{
    public void Configure(EntityTypeBuilder<FeatureDefinition> builder)
    {
        builder.HasKey(f => f.Id);
        builder.HasIndex(f => f.Code).IsUnique();
        builder.Property(f => f.Code).IsRequired().HasMaxLength(50);

        // Seed Data
        var fMaxWords = new Guid("f1111111-1111-1111-1111-111111111111");
        var fAiAccess = new Guid("f2222222-2222-2222-2222-222222222222");
        var fSupport = new Guid("f3333333-3333-3333-3333-333333333333");
        var fExport = new Guid("f4444444-4444-4444-4444-444444444444");

        builder.HasData(
            new FeatureDefinition { Id = fMaxWords, Code = "MAX_WORDS", Name = "Maximum Words", Description = "Limit of vocabulary words saved" },
            new FeatureDefinition { Id = fAiAccess, Code = "AI_ACCESS", Name = "AI Features", Description = "Access to AI analysis and generation" },
            new FeatureDefinition { Id = fSupport, Code = "SUPPORT_LEVEL", Name = "Support Level", Description = "Customer support priority" },
            new FeatureDefinition { Id = fExport, Code = "EXPORT_PDF", Name = "Export as PDF", Description = "Ability to export lists to PDF" }
        );
    }
}
