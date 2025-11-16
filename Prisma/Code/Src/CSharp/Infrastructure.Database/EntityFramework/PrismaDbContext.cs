using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Linq.Expressions;

namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework;

/// <summary>
/// Entity Framework Core database context for Prisma application.
/// Implements IPrismaDbContext for Infrastructure-internal abstraction and testability.
/// </summary>
public class PrismaDbContext : DbContext, IPrismaDbContext
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

    ///// <summary>
    ///// Gets the queryable for FileMetadata.
    ///// </summary>
    // public IQueryable<FileMetadata> FileMetadata => FileMetadataDbSet.AsQueryable();

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

    /// <summary>
    /// Gets the <see cref="DatabaseFacade"/> for this context.
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>

    /// DatabaseFacade
    ///
    public DatabaseFacade Database


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