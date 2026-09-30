using ExxerCube.Prisma.Domain.Models;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Service for batch processing multiple documents (XML + OCR + Comparison).
/// Designed for stakeholder demos with small batch sizes (max 4 documents).
/// </summary>
public interface IBulkProcessingService
{
    /// <summary>
    /// Gets a random sample of documents from the bulk directory.
    /// </summary>
    /// <param name="count">Number of documents to sample (max 4 for demo).</param>
    /// <param name="sourceDirectory">The directory containing bulk documents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of bulk documents ready for processing.</returns>
    Task<Result<List<BulkDocument>>> GetRandomSampleAsync(
        int count,
        string sourceDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a single document through XML extraction, OCR, and comparison.
    /// Uses Tesseract only (no GOT-OCR fallback) for speed.
    /// </summary>
    /// <param name="document">The document to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete processing result.</returns>
    Task<Result<BulkProcessingResult>> ProcessDocumentAsync(
        BulkDocument document,
        CancellationToken cancellationToken = default);
}
