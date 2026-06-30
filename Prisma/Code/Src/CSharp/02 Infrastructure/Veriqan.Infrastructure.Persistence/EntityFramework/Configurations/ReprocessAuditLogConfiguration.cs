using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework.Configurations;

/// <summary>
/// EF Core fluent configuration for <see cref="ReprocessAuditLogEntity"/>.
/// Rows are append-only; each row records one explicit reprocessing event.
/// </summary>
internal sealed class ReprocessAuditLogConfiguration
    : IEntityTypeConfiguration<ReprocessAuditLogEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReprocessAuditLogEntity> builder)
    {
        builder.ToTable("ReprocessAuditLog");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.ContentHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.Actor)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(e => e.BeforeSignal)
            .IsRequired(false);

        builder.Property(e => e.AfterSignal)
            .IsRequired();

        builder.Property(e => e.ReprocessedAtUtc)
            .IsRequired();

        builder.HasIndex(e => new { e.ContentHash, e.ReprocessedAtUtc })
            .HasDatabaseName("IX_ReprocessAuditLog_ContentHash");
    }
}
