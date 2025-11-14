using System;
using ExxerCube.Prisma.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Configurations;

/// <summary>
/// Entity Framework Core configuration for ReviewCase entity.
/// </summary>
public class ReviewCaseConfiguration : IEntityTypeConfiguration<ReviewCase>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReviewCase> builder)
    {
        builder.ToTable("ReviewCases");

        builder.HasKey(c => c.CaseId);

        builder.Property(c => c.CaseId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.FileId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.RequiresReviewReason)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.ConfidenceLevel)
            .IsRequired();

        builder.Property(c => c.ClassificationAmbiguity)
            .IsRequired();

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.AssignedTo)
            .HasMaxLength(100);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        // Foreign key relationship to FileMetadata
        builder.HasOne<FileMetadata>()
            .WithMany()
            .HasForeignKey(c => c.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(c => c.FileId)
            .HasDatabaseName("IX_ReviewCases_FileId");

        builder.HasIndex(c => c.Status)
            .HasDatabaseName("IX_ReviewCases_Status");

        builder.HasIndex(c => c.CreatedAt)
            .HasDatabaseName("IX_ReviewCases_CreatedAt");

        builder.HasIndex(c => c.AssignedTo)
            .HasDatabaseName("IX_ReviewCases_AssignedTo");
    }
}

