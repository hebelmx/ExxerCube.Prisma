using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework.Configurations;

/// <summary>
/// EF Core fluent configuration for the <see cref="Finding"/> entity.
/// </summary>
internal sealed class FindingConfiguration : IEntityTypeConfiguration<Finding>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Finding> builder)
    {
        builder.ToTable("Findings");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(f => f.VerificationJobId)
            .IsRequired();

        builder.HasIndex(f => f.VerificationJobId)
            .HasDatabaseName("IX_Findings_VerificationJobId");

        builder.Property(f => f.CheckId)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(f => f.CheckId)
            .HasDatabaseName("IX_Findings_CheckId");

        builder.Property(f => f.Verdict)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(f => f.Expected)
            .HasMaxLength(500);

        builder.Property(f => f.Observed)
            .HasMaxLength(500);

        builder.Property(f => f.EngineVersion)
            .HasMaxLength(50)
            .IsRequired();

        // Tier: regulatory tier this check belongs to (Story 1.4).
        // Stored as int; DB default = Condusef (1) for pre-migration / raw-SQL rows.
        //
        // HasSentinel(-1): EF's CLR sentinel defaults to the CLR default of the type (0 = Bank).
        // Without an explicit sentinel, EF treats Tier=Bank as "not set" and lets the DB default
        // (Condusef=1) override it, silently storing the wrong tier.  Setting the sentinel to
        // (ChecklistTier)(-1) — outside the valid enum range — forces EF to always include the
        // explicit Tier in every INSERT, regardless of which tier is assigned.
        builder.Property(f => f.Tier)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ChecklistTier.Condusef)
            .HasSentinel((ChecklistTier)(-1));
    }
}
