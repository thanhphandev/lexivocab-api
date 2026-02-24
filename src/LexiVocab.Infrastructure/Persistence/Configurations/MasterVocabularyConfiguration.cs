using LexiVocab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

public class MasterVocabularyConfiguration : IEntityTypeConfiguration<MasterVocabulary>
{
    public void Configure(EntityTypeBuilder<MasterVocabulary> builder)
    {
        builder.ToTable("master_vocabularies");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.Word)
            .HasColumnName("word")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(m => m.Word).IsUnique();

        builder.Property(m => m.PartOfSpeech)
            .HasColumnName("part_of_speech")
            .HasMaxLength(20);

        builder.Property(m => m.PhoneticUk)
            .HasColumnName("phonetic_uk")
            .HasMaxLength(100);

        builder.Property(m => m.PhoneticUs)
            .HasColumnName("phonetic_us")
            .HasMaxLength(100);

        builder.Property(m => m.AudioUrl)
            .HasColumnName("audio_url")
            .HasMaxLength(2048);

        builder.Property(m => m.PopularityRank)
            .HasColumnName("popularity_rank");

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
    }
}
