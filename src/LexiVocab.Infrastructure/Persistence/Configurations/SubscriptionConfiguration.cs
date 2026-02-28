using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.UserId).HasColumnName("user_id");

        builder.Property(s => s.Plan)
            .HasColumnName("plan")
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<SubscriptionPlan>(v))
            .HasDefaultValue(SubscriptionPlan.Free)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<SubscriptionStatus>(v))
            .HasDefaultValue(SubscriptionStatus.Active)
            .IsRequired();

        builder.Property(s => s.StartDate).HasColumnName("start_date").IsRequired();
        builder.Property(s => s.EndDate).HasColumnName("end_date");

        builder.Property(s => s.Provider)
            .HasColumnName("provider")
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<PaymentProvider>(v))
            .HasDefaultValue(PaymentProvider.Mock)
            .IsRequired();

        builder.Property(s => s.ExternalSubscriptionId)
            .HasColumnName("external_subscription_id")
            .HasMaxLength(255);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        // 1-N relationship with PaymentTransaction
        builder.HasMany(s => s.PaymentTransactions)
            .WithOne(p => p.Subscription)
            .HasForeignKey(p => p.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
