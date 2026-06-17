using ExxerCube.Prisma.Veriqan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework.Configurations;

/// <summary>
/// EF Core fluent configuration for the <see cref="VerificationJob"/> aggregate root.
/// All tables are homed in the <c>veriqan</c> schema via <see cref="VeriqanDbContext.OnModelCreating"/>.
/// </summary>
internal sealed class VerificationJobConfiguration : IEntityTypeConfiguration<VerificationJob>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VerificationJob> builder)
    {
        builder.ToTable("VerificationJobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(j => j.ContentHash)
            .HasMaxLength(64)
            .IsRequired();

        // ContentHash must be unique — duplicate content is rejected at the application layer.
        builder.HasIndex(j => j.ContentHash)
            .IsUnique()
            .HasDatabaseName("IX_VerificationJobs_ContentHash");

        builder.Property(j => j.ReceivedAtUtc)
            .IsRequired();

        builder.HasIndex(j => j.ReceivedAtUtc)
            .HasDatabaseName("IX_VerificationJobs_ReceivedAtUtc");

        builder.Property(j => j.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.HasIndex(j => j.Status)
            .HasDatabaseName("IX_VerificationJobs_Status");

        // 1 → N findings with cascade delete
        builder.HasMany(j => j.Findings)
            .WithOne()
            .HasForeignKey(f => f.VerificationJobId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1 → 0..1 rollup verdict
        builder.HasOne(j => j.Verdict)
            .WithOne()
            .HasForeignKey<JobVerdict>(v => v.VerificationJobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
