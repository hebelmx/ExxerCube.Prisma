using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework.Configurations;

/// <summary>
/// EF Core fluent configuration for <see cref="BatchExceptionLogEntity"/>.
/// Rows are mutable by design (RetryCount / LastRetryAt / ResolvedAt triage lifecycle) —
/// unlike <see cref="ReprocessAuditLogConfiguration"/>, this table is deliberately NOT added
/// to the <c>AddImmutabilityTriggers</c> migration's DENY UPDATE/DELETE set.
/// </summary>
/// <remarks>
/// The <c>2000</c> max length below is the literal value of
/// <c>ExxerCube.Prisma.Veriqan.Orchestration.Batch.BatchExceptionLogEntry.MaxFailureReasonLength</c>.
/// It cannot be referenced directly: this project (02 Infrastructure) is a dependency OF the
/// Orchestration project (03), so a reference the other way would be circular. Mirrors the same
/// literal-duplication pattern already used by <see cref="ReprocessAuditLogConfiguration"/> for
/// its own entity's constants.
/// </remarks>
internal sealed class BatchExceptionLogConfiguration
    : IEntityTypeConfiguration<BatchExceptionLogEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BatchExceptionLogEntity> builder)
    {
        builder.ToTable("BatchExceptionLog");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.BatchId)
            .IsRequired();

        builder.Property(e => e.StatementHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.InstitutionId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.FailureReason)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.FailedAt)
            .IsRequired();

        builder.Property(e => e.RetryCount)
            .IsRequired();

        builder.Property(e => e.LastRetryAt)
            .IsRequired(false);

        builder.Property(e => e.ResolvedAt)
            .IsRequired(false);

        builder.HasIndex(e => new { e.BatchId, e.FailedAt })
            .HasDatabaseName("IX_BatchExceptionLog_BatchId");
    }
}
