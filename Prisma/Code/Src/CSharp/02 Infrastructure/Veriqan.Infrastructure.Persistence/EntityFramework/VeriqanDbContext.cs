using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;

/// <summary>
/// Entity Framework Core database context for the Veriqan compliance-verification subsystem.
/// All tables are created in the <c>veriqan</c> SQL schema, fully isolated from
/// <c>PrismaDbContext</c> which owns the <c>dbo</c> schema.
/// The EF migrations history table is also placed in the <c>veriqan</c> schema
/// (<c>veriqan.__EFMigrationsHistory</c>) so the two contexts never collide.
/// </summary>
public sealed class VeriqanDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="VeriqanDbContext"/>.
    /// </summary>
    /// <param name="options">Database context options supplied by DI or the design-time factory.</param>
    public VeriqanDbContext(DbContextOptions<VeriqanDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets or sets the verification jobs aggregate-root set.</summary>
    public DbSet<VerificationJob> VerificationJobs { get; set; } = null!;

    /// <summary>Gets or sets the compliance findings entity set.</summary>
    public DbSet<Finding> Findings { get; set; } = null!;

    /// <summary>Gets or sets the rollup job-verdict entity set.</summary>
    public DbSet<JobVerdict> JobVerdicts { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Place ALL tables in the veriqan schema — this is the primary isolation boundary.
        modelBuilder.HasDefaultSchema("veriqan");

        modelBuilder.ApplyConfiguration(new VerificationJobConfiguration());
        modelBuilder.ApplyConfiguration(new FindingConfiguration());
        modelBuilder.ApplyConfiguration(new JobVerdictConfiguration());
    }
}
