using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.SubscriptionId).HasColumnName("subscription_id");
        builder.Property(p => p.UserId).HasColumnName("user_id");

        builder.Property(p => p.Provider)
            .HasColumnName("provider")
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<PaymentProvider>(v))
            .IsRequired();

        builder.Property(p => p.ExternalOrderId)
            .HasColumnName("external_order_id")
            .HasMaxLength(255)
            .IsRequired();

        // Idempotency: ensure external order ID is unique across everything
        builder.HasIndex(p => p.ExternalOrderId).IsUnique();

        builder.Property(p => p.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .HasDefaultValue("USD")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<PaymentStatus>(v))
            .HasDefaultValue(PaymentStatus.Pending)
            .IsRequired();

        builder.Property(p => p.PaidAt).HasColumnName("paid_at");

        builder.Property(p => p.RawPayload)
            .HasColumnName("raw_payload")
            .HasColumnType("text");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        
        // Navigation properties are configured on the other side (Subscription, User)
    }
}
