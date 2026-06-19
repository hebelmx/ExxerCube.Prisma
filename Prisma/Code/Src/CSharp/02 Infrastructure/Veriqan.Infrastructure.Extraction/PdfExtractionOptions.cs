namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;

/// <summary>
/// Configuration options for PDF-level safeguards in <see cref="PdfPigStatementFieldExtractor"/>.
/// </summary>
/// <remarks>
/// Bound from the <c>Veriqan:PdfExtraction</c> configuration section.
/// </remarks>
public sealed class PdfExtractionOptions
{
    /// <summary>Configuration section path.</summary>
    public const string Section = "Veriqan:PdfExtraction";

    /// <summary>
    /// Default maximum PDF file size in bytes (50 MB).
    /// PDFs larger than this value are rejected before <c>PdfDocument.Open</c> is called.
    /// </summary>
    public const long DefaultMaxSizeBytes = 50 * 1024 * 1024;

    /// <summary>
    /// Default parse timeout in seconds (30 s).
    /// If <c>ExtractFullAsync</c> exceeds this deadline the operation is cancelled and a
    /// <c>Timeout</c> failure result is returned.
    /// </summary>
    public const int DefaultParseTimeoutSeconds = 30;

    /// <summary>
    /// Maximum allowed PDF size in bytes. Defaults to <see cref="DefaultMaxSizeBytes"/> (50 MB).
    /// </summary>
    public long MaxSizeBytes { get; set; } = DefaultMaxSizeBytes;

    /// <summary>
    /// Maximum time in seconds allowed for a single PDF parse. Defaults to
    /// <see cref="DefaultParseTimeoutSeconds"/> (30 s).
    /// </summary>
    public int ParseTimeoutSeconds { get; set; } = DefaultParseTimeoutSeconds;
}
