using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using ExxerCube.Prisma.Domain.Entities;

namespace ExxerCube.Prisma.Infrastructure.Export.Adaptive.Data;

/// <summary>
/// Database context for adaptive template storage.
/// </summary>
public class TemplateDbContext : DbContext
{
    /// <summary>
    /// Gets or sets the templates DbSet.
    /// </summary>
    public DbSet<TemplateDefinition> Templates => Set<TemplateDefinition>();

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateDbContext"/> class.
    /// </summary>
    /// <param name="options">The database context options.</param>
    public TemplateDbContext(DbContextOptions<TemplateDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure TemplateDefinition entity
        modelBuilder.Entity<TemplateDefinition>(entity =>
        {
            entity.HasKey(e => e.TemplateId);

            entity.Property(e => e.TemplateType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Version)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.ModifiedBy)
                .HasMaxLength(100);

            // Ignore Dictionary property for InMemory provider
            // TODO: Switch to ToJson() in production with SQL Server
            entity.Ignore(e => e.Metadata);

            // Index for fast lookups
            entity.HasIndex(e => new { e.TemplateType, e.Version })
                .IsUnique();

            entity.HasIndex(e => new { e.TemplateType, e.IsActive, e.EffectiveDate });

            // Configure FieldMappings as owned collection
            // TODO: Switch to ToJson() in production - for now using in-memory table
            entity.OwnsMany(e => e.FieldMappings, fm =>
            {
                fm.Property(f => f.SourceFieldPath).IsRequired();
                fm.Property(f => f.TargetField).IsRequired();

                // Ignore collection/dictionary properties for InMemory provider
                fm.Ignore(f => f.Metadata);
                fm.Ignore(f => f.ValidationRules);
            });
        });
    }
}
