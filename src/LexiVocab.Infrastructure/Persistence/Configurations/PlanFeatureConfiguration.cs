using LexiVocab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
{
    public void Configure(EntityTypeBuilder<PlanFeature> builder)
    {
        builder.HasKey(pf => new { pf.PlanDefinitionId, pf.FeatureDefinitionId });

        builder.HasOne(pf => pf.Plan)
            .WithMany(p => p.PlanFeatures)
            .HasForeignKey(pf => pf.PlanDefinitionId);

        builder.HasOne(pf => pf.Feature)
            .WithMany(f => f.PlanFeatures)
            .HasForeignKey(pf => pf.FeatureDefinitionId);

        // Seed Mappings
        var freeId = new Guid("11111111-1111-1111-1111-111111111111");
        var premiumId = new Guid("22222222-2222-2222-2222-222222222222");
        var businessId = new Guid("33333333-3333-3333-3333-333333333333");

        var fMaxWords = new Guid("f1111111-1111-1111-1111-111111111111");
        var fAiAccess = new Guid("f2222222-2222-2222-2222-222222222222");
        var fSupport = new Guid("f3333333-3333-3333-3333-333333333333");
        var fExport = new Guid("f4444444-4444-4444-4444-444444444444");

        builder.HasData(
            // Free Tier
            new PlanFeature { PlanDefinitionId = freeId, FeatureDefinitionId = fMaxWords, Value = "50" },
            new PlanFeature { PlanDefinitionId = freeId, FeatureDefinitionId = fAiAccess, Value = "false" },
            new PlanFeature { PlanDefinitionId = freeId, FeatureDefinitionId = fSupport, Value = "Community" },
            new PlanFeature { PlanDefinitionId = freeId, FeatureDefinitionId = fExport, Value = "false" },

            // Premium Tier
            new PlanFeature { PlanDefinitionId = premiumId, FeatureDefinitionId = fMaxWords, Value = "1000" },
            new PlanFeature { PlanDefinitionId = premiumId, FeatureDefinitionId = fAiAccess, Value = "true" },
            new PlanFeature { PlanDefinitionId = premiumId, FeatureDefinitionId = fSupport, Value = "Email" },
            new PlanFeature { PlanDefinitionId = premiumId, FeatureDefinitionId = fExport, Value = "true" },

            // Business Tier
            new PlanFeature { PlanDefinitionId = businessId, FeatureDefinitionId = fMaxWords, Value = "Unlimited" },
            new PlanFeature { PlanDefinitionId = businessId, FeatureDefinitionId = fAiAccess, Value = "true" },
            new PlanFeature { PlanDefinitionId = businessId, FeatureDefinitionId = fSupport, Value = "24/7 Priority" },
            new PlanFeature { PlanDefinitionId = businessId, FeatureDefinitionId = fExport, Value = "true" }
        );
    }
}
