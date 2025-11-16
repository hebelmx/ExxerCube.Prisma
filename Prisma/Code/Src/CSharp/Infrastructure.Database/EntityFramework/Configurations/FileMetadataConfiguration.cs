namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Configurations;

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
            .HasConversion<int>();

        // Indexes for performance
        builder.HasIndex(f => f.Checksum)
            .HasDatabaseName("IX_FileMetadata_Checksum");

        builder.HasIndex(f => f.DownloadTimestamp)
            .HasDatabaseName("IX_FileMetadata_DownloadTimestamp");
    }
}

