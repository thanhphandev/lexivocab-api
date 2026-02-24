using LexiVocab.Domain.Entities;
using LexiVocab.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class ReviewLogConfiguration : IEntityTypeConfiguration<ReviewLog>
{
    public void Configure(EntityTypeBuilder<ReviewLog> builder)
    {
        builder.ToTable("review_logs");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.UserVocabularyId)
            .HasColumnName("user_vocabulary_id")
            .IsRequired();

        builder.Property(r => r.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(r => r.QualityScore)
            .HasColumnName("quality_score")
            .HasConversion<short>(); // SMALLINT in PostgreSQL

        builder.Property(r => r.TimeSpentMs)
            .HasColumnName("time_spent_ms");

        builder.Property(r => r.ReviewedAt)
            .HasColumnName("reviewed_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        // ─── INDEXES ──────────────────────────────────────────
        // Index on UserId for per-user analytics
        builder.HasIndex(r => r.UserId).HasDatabaseName("ix_review_logs_user_id");

        // Index on ReviewedAt for heatmap time-range queries
        // NOTE: EF Core doesn't support BRIN directly; use raw SQL migration for BRIN in production
        builder.HasIndex(r => r.ReviewedAt).HasDatabaseName("ix_review_logs_reviewed_at");

        // Composite for efficient user + date range queries
        builder.HasIndex(r => new { r.UserId, r.ReviewedAt })
            .HasDatabaseName("ix_review_logs_user_reviewed_at");
    }
}
