using LexiVocab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class PlanDefinitionConfiguration : IEntityTypeConfiguration<PlanDefinition>
{
    public void Configure(EntityTypeBuilder<PlanDefinition> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.Currency).HasMaxLength(5).HasDefaultValue("VND");

        // Seed Data
        var freeId = new Guid("11111111-1111-1111-1111-111111111111");
        var premiumId = new Guid("22222222-2222-2222-2222-222222222222");
        var businessId = new Guid("33333333-3333-3333-3333-333333333333");

        builder.HasData(
            new PlanDefinition
            {
                Id = freeId,
                Name = "Free",
                NameKey = "free_plan",
                Price = 0,
                Currency = "VND",
                Description = "Perfect for beginners",
                DurationDays = 0,
                IsRecommended = false
            },
            new PlanDefinition
            {
                Id = premiumId,
                Name = "Premium",
                NameKey = "premium_plan",
                Price = 199000,
                Currency = "VND",
                Description = "Unlock full potential",
                DurationDays = 30,
                IsRecommended = true
            },
            new PlanDefinition
            {
                Id = businessId,
                Name = "Business",
                NameKey = "business_plan",
                Price = 999000,
                Currency = "VND",
                Description = "For advanced learners and teams",
                DurationDays = 365,
                IsRecommended = false
            }
        );
    }
}
