using ExxerCube.Prisma.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Configurations;

/// <summary>
/// Entity Framework Core configuration for AuditRecord entity.
/// </summary>
public class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("AuditRecords");

        builder.HasKey(a => a.AuditId);

        builder.Property(a => a.AuditId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.CorrelationId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.FileId)
            .HasMaxLength(100);

        builder.Property(a => a.ActionType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.ActionDetails)
            .HasMaxLength(4000);

        builder.Property(a => a.UserId)
            .HasMaxLength(100);

        builder.Property(a => a.Timestamp)
            .IsRequired();

        builder.Property(a => a.Stage)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.Success)
            .IsRequired();

        builder.Property(a => a.ErrorMessage)
            .HasMaxLength(1000);

        // Foreign key relationship to FileMetadata (optional)
        builder.HasOne<FileMetadata>()
            .WithMany()
            .HasForeignKey(a => a.FileId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for performance
        builder.HasIndex(a => a.FileId)
            .HasDatabaseName("IX_AuditRecords_FileId");

        builder.HasIndex(a => a.Timestamp)
            .HasDatabaseName("IX_AuditRecords_Timestamp");

        builder.HasIndex(a => a.ActionType)
            .HasDatabaseName("IX_AuditRecords_ActionType");

        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("IX_AuditRecords_UserId");

        builder.HasIndex(a => a.CorrelationId)
            .HasDatabaseName("IX_AuditRecords_CorrelationId");

        // Composite index for common query patterns
        builder.HasIndex(a => new { a.FileId, a.Timestamp })
            .HasDatabaseName("IX_AuditRecords_FileId_Timestamp");
    }
}

