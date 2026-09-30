using ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Configurations;

/// <summary>
/// Entity Framework Core configuration for <see cref="PersistedUnifiedMetadata"/>.
/// </summary>
public class PersistedUnifiedMetadataConfiguration : IEntityTypeConfiguration<PersistedUnifiedMetadata>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PersistedUnifiedMetadata> builder)
    {
        builder.ToTable("UnifiedMetadataRecords");

        builder.HasKey(e => e.FileId);

        builder.Property(e => e.FileId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.PayloadJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        builder.HasIndex(e => e.UpdatedAt)
            .HasDatabaseName("IX_UnifiedMetadataRecords_UpdatedAt");
    }
}
