using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework.Configurations;

/// <summary>
/// EF Core fluent configuration for <see cref="VerificationOutcomeSnapshotEntity"/>.
/// Each row stores the full JSON snapshot of one <c>VerificationOutcome</c>,
/// keyed by the document content hash (SHA-256, 64 hex chars).
/// </summary>
internal sealed class VerificationOutcomeSnapshotConfiguration
    : IEntityTypeConfiguration<VerificationOutcomeSnapshotEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VerificationOutcomeSnapshotEntity> builder)
    {
        builder.ToTable("VerificationOutcomeSnapshots");

        builder.HasKey(e => e.ContentHash);

        builder.Property(e => e.ContentHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.OutcomeJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(e => e.SavedAtUtc)
            .IsRequired();

        builder.Property(e => e.ReplacedAtUtc)
            .IsRequired(false);
    }
}
