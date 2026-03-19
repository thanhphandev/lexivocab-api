using LexiVocab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class FeatureDefinitionConfiguration : IEntityTypeConfiguration<FeatureDefinition>
{
    public void Configure(EntityTypeBuilder<FeatureDefinition> builder)
    {
        builder.ToTable("feature_definitions");

        builder.HasKey(f => f.Id);
        builder.HasIndex(f => f.Code).HasDatabaseName("ix_feature_definitions_code").IsUnique();
        builder.Property(f => f.Code).IsRequired().HasMaxLength(50);

        // Seed data moved to FeatureDefinitionSeeder.cs
    }
}
