namespace ExxerCube.Prisma.Infrastructure.FileStorage;

/// <summary>
/// Configuration options for file storage.
/// </summary>
public class FileStorageOptions
{
    /// <summary>
    /// Gets or sets the base path for file storage.
    /// </summary>
    public string StorageBasePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base storage path for classified/organized files.
    /// </summary>
    public string BaseStoragePath { get; set; } = "Storage/Classified";
}