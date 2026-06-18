using ExxerCube.Prisma.Veriqan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework.Configurations;

/// <summary>
/// EF Core fluent configuration for the <see cref="Disposition"/> entity.
/// </summary>
/// <remarks>
/// The <c>Dispositions</c> table is placed in the <c>veriqan</c> schema (inherited from the
/// context-level <c>HasDefaultSchema("veriqan")</c>) and is insert-only — no update or
/// delete operations should ever target it.
/// </remarks>
internal sealed class DispositionConfiguration : IEntityTypeConfiguration<Disposition>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Disposition> builder)
    {
        builder.ToTable("Dispositions");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(d => d.VerificationJobId)
            .IsRequired();

        builder.HasIndex(d => d.VerificationJobId)
            .HasDatabaseName("IX_Dispositions_VerificationJobId");

        builder.Property(d => d.FindingId);

        builder.HasIndex(d => d.FindingId)
            .HasDatabaseName("IX_Dispositions_FindingId");

        builder.Property(d => d.Action)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(d => d.Actor)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(d => d.DispositionedAtUtc)
            .IsRequired();

        builder.Property(d => d.BeforeState)
            .HasMaxLength(1000);

        builder.Property(d => d.AfterState)
            .HasMaxLength(1000);

        builder.Property(d => d.Notes)
            .HasMaxLength(2000);

        builder.Property(d => d.EngineVersion)
            .HasMaxLength(50);

        builder.Property(d => d.ReferenceBundleVersion)
            .HasMaxLength(50);
    }
}
