namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Represents the result of metadata extraction processing.
/// </summary>
public class MetadataExtractionResult
{
    /// <summary>
    /// Gets or sets the original file path.
    /// </summary>
    public string OriginalFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the new organized file path.
    /// </summary>
    public string NewFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the classification result.
    /// </summary>
    public ClassificationResult Classification { get; set; } = new();

    /// <summary>
    /// Gets or sets the extracted metadata.
    /// </summary>
    public ExtractedMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the identified file format.
    /// </summary>
    public FileFormat FileFormat { get; set; } = FileFormat.Unknown;
}
