namespace ExxerCube.Prisma.Domain.Sources;

/// <summary>
/// Represents a text source for field extraction (typically OCR output).
/// </summary>
/// <remarks>
/// <para>
/// This source type is designed to work with OCR-extracted text from scanned documents.
/// It includes metadata about OCR quality and confidence to support adaptive extraction strategies.
/// </para>
/// <para>
/// <strong>Typical Use Cases:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>OCR output from scanned PDF documents</description></item>
///   <item><description>OCR output from scanned image files (JPG, PNG, TIFF)</description></item>
///   <item><description>Pre-extracted text requiring field extraction</description></item>
/// </list>
/// </remarks>
public class TxtSource
{
    /// <summary>
    /// Gets or sets the text content to extract from.
    /// </summary>
    public string TextContent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source file path (if available, for logging/debugging).
    /// </summary>
    public string? SourceFilePath { get; set; }

    /// <summary>
    /// Gets or sets the OCR confidence score (if this text came from OCR).
    /// Range: 0.0 (no confidence) to 1.0 (perfect confidence).
    /// </summary>
    public float? OcrConfidence { get; set; }

    /// <summary>
    /// Gets or sets the image quality score (if this text came from scanned image).
    /// Range: 0.0 (poor quality) to 1.0 (excellent quality).
    /// </summary>
    public float? ImageQualityScore { get; set; }

    /// <summary>
    /// Gets or sets metadata about the source (e.g., page numbers, source type, processing info).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TxtSource"/> class.
    /// </summary>
    public TxtSource()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TxtSource"/> class with text content.
    /// </summary>
    /// <param name="textContent">The text content.</param>
    /// <param name="ocrConfidence">Optional OCR confidence score.</param>
    /// <param name="sourceFilePath">Optional source file path.</param>
    public TxtSource(string textContent, float? ocrConfidence = null, string? sourceFilePath = null)
    {
        TextContent = textContent;
        OcrConfidence = ocrConfidence;
        SourceFilePath = sourceFilePath;
    }
}
