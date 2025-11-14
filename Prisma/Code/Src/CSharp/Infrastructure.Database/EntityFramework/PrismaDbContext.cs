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

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new FileMetadataConfiguration());
    }
}

