using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework;

/// <summary>
/// Entity Framework Core database context for Prisma application.
/// </summary>
public class PrismaDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PrismaDbContext"/> class.
    /// </summary>
    /// <param name="options">The database context options.</param>
    public PrismaDbContext(DbContextOptions<PrismaDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the FileMetadata entity set.
    /// </summary>
    public DbSet<FileMetadata> FileMetadata { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Persona entity set.
    /// </summary>
    public DbSet<Persona> Persona { get; set; } = null!;

    /// <summary>
    /// Gets or sets the SLAStatus entity set.
    /// </summary>
    public DbSet<SLAStatus> SLAStatus { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ReviewCases entity set.
    /// </summary>
    public DbSet<ReviewCase> ReviewCases { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ReviewDecisions entity set.
    /// </summary>
    public DbSet<ReviewDecision> ReviewDecisions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the AuditRecords entity set.
    /// </summary>
    public DbSet<AuditRecord> AuditRecords { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new FileMetadataConfiguration());
        modelBuilder.ApplyConfiguration(new PersonaConfiguration());
        modelBuilder.ApplyConfiguration(new SLAStatusConfiguration());
        modelBuilder.ApplyConfiguration(new ReviewCaseConfiguration());
        modelBuilder.ApplyConfiguration(new ReviewDecisionConfiguration());
        modelBuilder.ApplyConfiguration(new AuditRecordConfiguration());
    }
}

