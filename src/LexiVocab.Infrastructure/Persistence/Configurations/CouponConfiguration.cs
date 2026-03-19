using LexiVocab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("coupons");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.Property(x => x.DiscountType)
            .IsRequired();

        builder.Property(x => x.DiscountValue)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        // One coupon can be used in many PaymentTransactions
        builder.HasMany(x => x.Transactions)
            .WithOne(t => t.Coupon)
            .HasForeignKey(t => t.CouponId)
            .OnDelete(DeleteBehavior.SetNull); // If coupon is deleted, we just set the FK to null (or we could Restrict)
    }
}
