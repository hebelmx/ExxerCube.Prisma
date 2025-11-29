namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Configurations;

using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Enums;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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
            .HasConversion(
                v => v.Value,
                v => AuditActionType.FromValue(v))
            .Metadata.SetValueComparer(new ValueComparer<AuditActionType>(
                (l, r) => (l ?? AuditActionType.Unknown).Value == (r ?? AuditActionType.Unknown).Value,
                v => (v ?? AuditActionType.Unknown).Value.GetHashCode(),
                v => AuditActionType.FromValue((v ?? AuditActionType.Unknown).Value)));

        builder.Property(a => a.ActionDetails)
            .HasMaxLength(4000);

        builder.Property(a => a.UserId)
            .HasMaxLength(100);

        builder.Property(a => a.Timestamp)
            .IsRequired();

        builder.Property(a => a.Stage)
            .IsRequired()
            .HasConversion(
                v => v.Value,
                v => ProcessingStage.FromValue(v))
            .Metadata.SetValueComparer(new ValueComparer<ProcessingStage>(
                (l, r) => (l ?? ProcessingStage.Unknown).Value == (r ?? ProcessingStage.Unknown).Value,
                v => (v ?? ProcessingStage.Unknown).Value.GetHashCode(),
                v => ProcessingStage.FromValue((v ?? ProcessingStage.Unknown).Value)));

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

