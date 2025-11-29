namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the response exporter service for generating SIRO-compliant XML and PDF exports from unified metadata records.
/// </summary>
public interface IResponseExporter
{
    /// <summary>
    /// Exports unified metadata record to SIRO-compliant XML format.
    /// </summary>
    /// <param name="metadata">The unified metadata record to export.</param>
    /// <param name="outputStream">The output stream to write the XML content.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> ExportSiroXmlAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports unified metadata record to digitally signed PDF format (PAdES standard).
    /// </summary>
    /// <param name="metadata">The unified metadata record to export.</param>
    /// <param name="outputStream">The output stream to write the PDF content.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> ExportSignedPdfAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        CancellationToken cancellationToken = default);
}

