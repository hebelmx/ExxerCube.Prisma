using ExxerCube.Prisma.Veriqan.Domain.Entities;
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
    }
}
