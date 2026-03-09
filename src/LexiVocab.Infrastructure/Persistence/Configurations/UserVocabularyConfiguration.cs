using LexiVocab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class UserVocabularyConfiguration : IEntityTypeConfiguration<UserVocabulary>
{
    public void Configure(EntityTypeBuilder<UserVocabulary> builder)
    {
        builder.ToTable("user_vocabularies");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");

        builder.Property(v => v.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(v => v.MasterVocabularyId).HasColumnName("master_vocabulary_id");
        builder.Property(v => v.TagId).HasColumnName("tag_id");

        builder.Property(v => v.WordText)
            .HasColumnName("word_text")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(v => v.CustomMeaning)
            .HasColumnName("custom_meaning")
            .HasMaxLength(500);

        builder.Property(v => v.ContextSentence)
            .HasColumnName("context_sentence")
            .HasColumnType("text");

        builder.Property(v => v.SourceUrl)
            .HasColumnName("source_url")
            .HasMaxLength(2048);

        // ─── SM-2 Fields ──────────────────────────────────────
        builder.Property(v => v.RepetitionCount)
            .HasColumnName("repetition_count")
            .HasDefaultValue(0);

        builder.Property(v => v.EasinessFactor)
            .HasColumnName("easiness_factor")
            .HasDefaultValue(2.5);

        builder.Property(v => v.IntervalDays)
            .HasColumnName("interval_days")
            .HasDefaultValue(0);

        builder.Property(v => v.NextReviewDate)
            .HasColumnName("next_review_date");

        builder.Property(v => v.LastReviewedAt)
            .HasColumnName("last_reviewed_at");

        builder.Property(v => v.IsArchived)
            .HasColumnName("is_archived")
            .HasDefaultValue(false);

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");

        // ─── CRITICAL INDEXES ─────────────────────────────────
        // Index on UserId — fast per-user vocabulary list
        builder.HasIndex(v => v.UserId).HasDatabaseName("ix_user_vocabularies_user_id");

        // Index on NextReviewDate — sub-ms daily review queries
        builder.HasIndex(v => v.NextReviewDate).HasDatabaseName("ix_user_vocabularies_next_review_date");

        // Composite index for the most critical query: user's due flashcards
        builder.HasIndex(v => new { v.UserId, v.NextReviewDate, v.IsArchived })
            .HasDatabaseName("ix_user_vocabularies_user_review_archive");

        // ─── Relationships ────────────────────────────────────
        builder.HasOne(v => v.Tag)
            .WithMany(t => t.UserVocabularies)
            .HasForeignKey(v => v.TagId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(v => v.MasterVocabulary)
            .WithMany(m => m.UserVocabularies)
            .HasForeignKey(v => v.MasterVocabularyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(v => v.ReviewLogs)
            .WithOne(r => r.UserVocabulary)
            .HasForeignKey(r => r.UserVocabularyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
