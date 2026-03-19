using LexiVocab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
{
    public void Configure(EntityTypeBuilder<PlanFeature> builder)
    {
        builder.ToTable("plan_features");

        builder.HasKey(pf => new { pf.PlanDefinitionId, pf.FeatureDefinitionId });

        builder.HasOne(pf => pf.Plan)
            .WithMany(p => p.PlanFeatures)
            .HasForeignKey(pf => pf.PlanDefinitionId);

        builder.HasOne(pf => pf.Feature)
            .WithMany(f => f.PlanFeatures)
            .HasForeignKey(pf => pf.FeatureDefinitionId);

        // Seed data moved to PlanDefinitionSeeder.cs
    }
}
