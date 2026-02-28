using LexiVocab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexiVocab.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for AuditLog.
/// Optimized for high-throughput writes and indexed queries on common filter columns.
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever(); // Application-generated UUIDs

        builder.Property(a => a.UserEmail)
            .HasMaxLength(256);

        builder.Property(a => a.Action)
            .HasConversion<string>() // Store as human-readable string for easy querying
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .HasMaxLength(100);

        builder.Property(a => a.OldValues)
            .HasColumnType("text"); // Unbounded JSON

        builder.Property(a => a.NewValues)
            .HasColumnType("text"); // Unbounded JSON

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45); // IPv6 max length

        builder.Property(a => a.UserAgent)
            .HasMaxLength(512);

        builder.Property(a => a.RequestName)
            .HasMaxLength(200);

        builder.Property(a => a.TraceId)
            .HasMaxLength(128);

        builder.Property(a => a.AdditionalInfo)
            .HasMaxLength(2000);

        builder.Property(a => a.Timestamp)
            .IsRequired()
            .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");

        // ─── Indexes for common query patterns ───────────────
        // UserId + Timestamp: "Show me user X's activity in the last 7 days"
        builder.HasIndex(a => new { a.UserId, a.Timestamp })
            .HasDatabaseName("IX_AuditLogs_UserId_Timestamp")
            .IsDescending(false, true);

        // Action + Timestamp: "Show all failed logins today"
        builder.HasIndex(a => new { a.Action, a.Timestamp })
            .HasDatabaseName("IX_AuditLogs_Action_Timestamp")
            .IsDescending(false, true);

        // IpAddress + Timestamp: brute-force detection
        builder.HasIndex(a => new { a.IpAddress, a.Timestamp })
            .HasDatabaseName("IX_AuditLogs_IpAddress_Timestamp")
            .IsDescending(false, true);

        // Timestamp alone for date-range scans
        builder.HasIndex(a => a.Timestamp)
            .HasDatabaseName("IX_AuditLogs_Timestamp")
            .IsDescending(true);

        // ─── Relationship ────────────────────────────────────
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull); // Keep audit logs even if user is deleted
    }
}
