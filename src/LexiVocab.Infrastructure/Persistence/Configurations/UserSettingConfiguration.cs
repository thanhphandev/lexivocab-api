using LexiVocab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class UserSettingConfiguration : IEntityTypeConfiguration<UserSetting>
{
    public void Configure(EntityTypeBuilder<UserSetting> builder)
    {
        builder.ToTable("user_settings");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        // 1-1 unique constraint
        builder.HasIndex(s => s.UserId).IsUnique();

        builder.Property(s => s.IsHighlightEnabled)
            .HasColumnName("is_highlight_enabled")
            .HasDefaultValue(true);

        builder.Property(s => s.HighlightColor)
            .HasColumnName("highlight_color")
            .HasMaxLength(20)
            .HasDefaultValue("#FFD700");

        // JSONB column for flexible excluded domains storage
        builder.Property(s => s.ExcludedDomains)
            .HasColumnName("excluded_domains")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(s => s.DailyGoal)
            .HasColumnName("daily_goal")
            .HasDefaultValue(20);

        builder.Property(s => s.PreferencesJson)
            .HasColumnName("preferences_json")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
    }
}
