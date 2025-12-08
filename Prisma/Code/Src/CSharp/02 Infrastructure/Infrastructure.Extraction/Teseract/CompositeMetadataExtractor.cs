namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

/// <summary>
/// Composite metadata extractor that delegates to the appropriate format-specific extractor.
/// </summary>
public class CompositeMetadataExtractor : IMetadataExtractor
{
    private readonly XmlMetadataExtractor _xmlExtractor;
    private readonly DocxMetadataExtractor _docxExtractor;
    private readonly PdfMetadataExtractor _pdfExtractor;
    private readonly ILogger<CompositeMetadataExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeMetadataExtractor"/> class.
    /// </summary>
    /// <param name="xmlExtractor">The XML metadata extractor.</param>
    /// <param name="docxExtractor">The DOCX metadata extractor.</param>
    /// <param name="pdfExtractor">The PDF metadata extractor.</param>
    /// <param name="logger">The logger instance.</param>
    public CompositeMetadataExtractor(
        XmlMetadataExtractor xmlExtractor,
        DocxMetadataExtractor docxExtractor,
        PdfMetadataExtractor pdfExtractor,
        ILogger<CompositeMetadataExtractor> logger)
    {
        _xmlExtractor = xmlExtractor;
        _docxExtractor = docxExtractor;
        _pdfExtractor = pdfExtractor;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<ExtractedMetadata>> ExtractFromXmlAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        return _xmlExtractor.ExtractFromXmlAsync(fileContent, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<ExtractedMetadata>> ExtractFromDocxAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        return _docxExtractor.ExtractFromDocxAsync(fileContent, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<ExtractedMetadata>> ExtractFromPdfAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        return _pdfExtractor.ExtractFromPdfAsync(fileContent, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<string>> ExtractTextAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        // Delegate to PDF extractor for text extraction (most common use case)
        // In a production system, you might want to detect file type first
        return _pdfExtractor.ExtractTextAsync(fileContent, cancellationToken);
    }
}