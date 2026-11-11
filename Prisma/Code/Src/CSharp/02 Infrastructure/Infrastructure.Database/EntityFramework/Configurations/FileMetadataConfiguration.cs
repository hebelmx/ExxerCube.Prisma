namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Configurations;

using ExxerCube.Prisma.Domain.Enum;
using Microsoft.EntityFrameworkCore.ChangeTracking;

/// <summary>
/// Entity Framework Core configuration for FileMetadata entity.
/// </summary>
public class FileMetadataConfiguration : IEntityTypeConfiguration<FileMetadata>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FileMetadata> builder)
    {
        builder.ToTable("FileMetadata");

        builder.HasKey(f => f.FileId);

        builder.Property(f => f.FileId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.FileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(f => f.FilePath)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(f => f.Url)
            .HasMaxLength(2000);

        builder.Property(f => f.DownloadTimestamp)
            .IsRequired();

        builder.Property(f => f.Checksum)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(f => f.FileSize)
            .IsRequired();

        builder.Property(f => f.Format)
            .IsRequired()
            .HasConversion(
                v => v.Value,
                v => FileFormat.FromValue(v))
            .Metadata.SetValueComparer(new ValueComparer<FileFormat>(
                (l, r) => (l ?? FileFormat.Unknown).Value == (r ?? FileFormat.Unknown).Value,
                v => (v ?? FileFormat.Unknown).Value.GetHashCode(),
                v => FileFormat.FromValue((v ?? FileFormat.Unknown).Value)));

        // Indexes for performance
        builder.HasIndex(f => f.Checksum)
            .HasDatabaseName("IX_FileMetadata_Checksum");

        builder.HasIndex(f => f.DownloadTimestamp)
            .HasDatabaseName("IX_FileMetadata_DownloadTimestamp");
    }
}

