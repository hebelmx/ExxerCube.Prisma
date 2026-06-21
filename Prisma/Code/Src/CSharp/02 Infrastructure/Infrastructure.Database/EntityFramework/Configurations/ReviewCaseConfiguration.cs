using ExxerCube.Prisma.Domain.Enum;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Configurations;

/// <summary>
/// Entity Framework Core configuration for ReviewCase entity.
/// </summary>
public class ReviewCaseConfiguration : IEntityTypeConfiguration<ReviewCase>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReviewCase> builder)
    {
        var reviewReasonComparer = new ValueComparer<ReviewReason>(
            (l, r) => (l ?? ReviewReason.Unknown).Value == (r ?? ReviewReason.Unknown).Value,
            v => (v ?? ReviewReason.Unknown).Value.GetHashCode(),
            v => ReviewReason.FromValue((v ?? ReviewReason.Unknown).Value));

        var reviewStatusComparer = new ValueComparer<ReviewStatus>(
            (l, r) => (l ?? ReviewStatus.Unknown).Value == (r ?? ReviewStatus.Unknown).Value,
            v => (v ?? ReviewStatus.Unknown).Value.GetHashCode(),
            v => ReviewStatus.FromValue((v ?? ReviewStatus.Unknown).Value));

        builder.ToTable("ReviewCases");

        builder.HasKey(c => c.CaseId);

        builder.Property(c => c.CaseId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.FileId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.RequiresReviewReason)
            .IsRequired()
            .HasConversion(
                v => v.Value,
                v => ReviewReason.FromValue(v))
            .Metadata.SetValueComparer(reviewReasonComparer);

        builder.Property(c => c.ConfidenceLevel)
            .IsRequired();

        builder.Property(c => c.ClassificationAmbiguity)
            .IsRequired();

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion(
                v => v.Value,
                v => ReviewStatus.FromValue(v))
            .Metadata.SetValueComparer(reviewStatusComparer);

        builder.Property(c => c.AssignedTo)
            .HasMaxLength(100);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        // G-C2b: storage-relative path of the fused expediente handoff artifact.
        // Nullable — only populated in the 3-process split when the export gate blocks the case.
        builder.Property(c => c.HandoffPath)
            .HasMaxLength(500)
            .IsRequired(false);

        // No referential constraint to FileMetadata: worker processes (the 3-process split) persist
        // ReviewCases keyed by FileId BEFORE a FileMetadata row exists, so an FK caused every worker
        // INSERT to fail and the review case to be silently dropped (fail-open) — the dashboard then
        // showed nothing from the real pipeline. FileId stays a plain indexed column. This mirrors the
        // owner-approved decision already applied to AuditRecords (see DropAuditFileMetadataFk).

        // Indexes for performance
        builder.HasIndex(c => c.FileId)
            .HasDatabaseName("IX_ReviewCases_FileId");

        builder.HasIndex(c => c.Status)
            .HasDatabaseName("IX_ReviewCases_Status");

        builder.HasIndex(c => c.CreatedAt)
            .HasDatabaseName("IX_ReviewCases_CreatedAt");

        builder.HasIndex(c => c.AssignedTo)
            .HasDatabaseName("IX_ReviewCases_AssignedTo");
    }
}

