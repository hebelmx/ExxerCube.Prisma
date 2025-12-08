namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the metadata extractor service for extracting metadata from multiple document formats (XML, DOCX, PDF).
/// Wraps existing OCR functionality for PDF processing to maintain compatibility.
/// </summary>
public interface IMetadataExtractor
{
    /// <summary>
    /// Extracts metadata from XML documents (expediente number, oficio number, RFC, names, dates, legal references).
    /// </summary>
    /// <param name="fileContent">The XML file content as a byte array.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the extracted metadata or an error.</returns>
    Task<Result<ExtractedMetadata>> ExtractFromXmlAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts metadata from DOCX documents using structured field extraction.
    /// </summary>
    /// <param name="fileContent">The DOCX file content as a byte array.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the extracted metadata or an error.</returns>
    Task<Result<ExtractedMetadata>> ExtractFromDocxAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts metadata from PDF documents with OCR fallback using existing OCR pipeline.
    /// Detects scanned PDFs and applies image preprocessing before OCR.
    /// </summary>
    /// <param name="fileContent">The PDF file content as a byte array.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the extracted metadata or an error.</returns>
    Task<Result<ExtractedMetadata>> ExtractFromPdfAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts plain text content from PDF documents using OCR fallback.
    /// This method is used for text extraction when full metadata extraction is not needed.
    /// </summary>
    /// <param name="fileContent">The PDF file content as a byte array.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the extracted text content or an error.</returns>
    Task<Result<string>> ExtractTextAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default);
}

