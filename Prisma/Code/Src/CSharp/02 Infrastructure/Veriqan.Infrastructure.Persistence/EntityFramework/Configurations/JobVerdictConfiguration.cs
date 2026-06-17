using ExxerCube.Prisma.Veriqan.Domain.Entities;
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
    }
}
