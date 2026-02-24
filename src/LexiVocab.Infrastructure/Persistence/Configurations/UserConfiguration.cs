using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255);

        builder.Property(u => u.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.Property(u => u.LastLogin).HasColumnName("last_login");

        builder.Property(u => u.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<UserRole>(v))
            .HasDefaultValue(UserRole.User);

        builder.Property(u => u.AuthProvider)
            .HasColumnName("auth_provider")
            .HasMaxLength(50);

        builder.Property(u => u.AuthProviderId)
            .HasColumnName("auth_provider_id")
            .HasMaxLength(255);

        builder.Property(u => u.RefreshTokenHash)
            .HasColumnName("refresh_token_hash")
            .HasMaxLength(255);

        builder.Property(u => u.RefreshTokenExpiryTime)
            .HasColumnName("refresh_token_expiry_time");

        // 1-1 relationship with UserSetting
        builder.HasOne(u => u.UserSetting)
            .WithOne(s => s.User)
            .HasForeignKey<UserSetting>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1-N relationship with UserVocabulary
        builder.HasMany(u => u.UserVocabularies)
            .WithOne(v => v.User)
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1-N relationship with ReviewLog (denormalized)
        builder.HasMany(u => u.ReviewLogs)
            .WithOne(r => r.User)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
