using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework.Configurations;

/// <summary>
/// EF Core fluent configuration for the <see cref="JobVerdict"/> rollup entity.
/// </summary>
internal sealed class JobVerdictConfiguration : IEntityTypeConfiguration<JobVerdict>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<JobVerdict> builder)
    {
        builder.ToTable("JobVerdicts");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(v => v.VerificationJobId)
            .IsRequired();

        // VerificationJobId is the FK to VerificationJob (1-to-1), also unique.
        builder.HasIndex(v => v.VerificationJobId)
            .IsUnique()
            .HasDatabaseName("IX_JobVerdicts_VerificationJobId");

        builder.Property(v => v.Signal)
            .IsRequired()
            .HasConversion<int>();

        // BankTierVerdict / CondusefTierVerdict: two-tier verdict columns (Story 1.4).
        // Stored as int (same convention as Signal).
        // Default VerdictSignal.Green so pre-migration rows read as Green after the
        // AddTwoTierVerdictColumns migration runs (int 0 in the generated DDL).
        builder.Property(v => v.BankTierVerdict)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(VerdictSignal.Green);

        builder.Property(v => v.CondusefTierVerdict)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(VerdictSignal.Green);

        // AlertSentAt: nullable dedup flag — null = not yet sent; set = already dispatched.
        // Configured as a concurrency token so two concurrent retries racing to set it raise
        // a DbUpdateConcurrencyException and only one commit wins (Story E2-S15).
        builder.Property(v => v.AlertSentAt)
            .IsRequired(false)
            .HasColumnType("datetimeoffset")
            .IsConcurrencyToken();

        // Confidence: minimum extraction confidence across all findings for this verdict (Story 4.1).
        // Stored as SQL float (double-precision). DB default = 1.0 for pre-migration rows.
        //
        // HasSentinel(-1.0): same sentinel pattern as Finding.Confidence — prevents EF from
        // treating a legitimate confidence of 0.0 as "not set" and silently promoting it to the
        // DB default of 1.0.
        builder.Property(v => v.Confidence)
            .IsRequired()
            .HasColumnType("float")
            .HasDefaultValue(1.0)
            .HasSentinel(-1.0);

        // EngineVersion: semantic version of the verification engine that produced this verdict
        // (VERIQAN-E3-S4). DB default = "unknown" for pre-migration rows. Max length 50 mirrors
        // Finding.EngineVersion / Disposition.EngineVersion.
        builder.Property(v => v.EngineVersion)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("unknown");

        // ReferenceBundleVersion: version of the reference-data bundle active when this verdict
        // was computed (VERIQAN-E3-S4). Nullable — the pipeline's catalog pre-resolve stage
        // degrades gracefully to a null bundle when no bundle is resolved for the submission's
        // context key, so this column has no non-null invariant to enforce. Max length 50
        // mirrors Disposition.ReferenceBundleVersion.
        builder.Property(v => v.ReferenceBundleVersion)
            .HasMaxLength(50);
    }
}
