namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents the status of a document in bulk processing.
/// </summary>
public enum BulkProcessingStatus
{
    /// <summary>
    /// Document is queued and waiting to be processed.
    /// </summary>
    Pending,

    /// <summary>
    /// Currently processing XML extraction.
    /// </summary>
    ProcessingXml,

    /// <summary>
    /// Currently processing OCR extraction from PDF.
    /// </summary>
    ProcessingOcr,

    /// <summary>
    /// Currently comparing XML and OCR results.
    /// </summary>
    Comparing,

    /// <summary>
    /// Processing completed successfully.
    /// </summary>
    Complete,

    /// <summary>
    /// Processing failed with an error.
    /// </summary>
    Error
}