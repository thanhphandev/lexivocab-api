using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class PlanPricingConfiguration : IEntityTypeConfiguration<PlanPricing>
{
    public void Configure(EntityTypeBuilder<PlanPricing> builder)
    {
        builder.ToTable("plan_pricings");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.BillingCycle)
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<BillingCycle>(v))
            .IsRequired();

        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.Currency).HasMaxLength(5).HasDefaultValue("VND");
        builder.Property(p => p.LabelKey).HasMaxLength(50);
        
        builder.HasOne(p => p.Plan)
            .WithMany(pl => pl.Pricings)
            .HasForeignKey(p => p.PlanDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
