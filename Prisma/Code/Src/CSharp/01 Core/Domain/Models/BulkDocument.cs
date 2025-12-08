namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents a document in the bulk processing queue.
/// Contains paths to both XML and PDF twins for comparison.
/// </summary>
public class BulkDocument
{
    /// <summary>
    /// Gets or sets the document identifier (base filename without extension).
    /// Example: "222AAA-44444444442025"
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full path to the XML file.
    /// </summary>
    public string XmlPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full path to the PDF file.
    /// </summary>
    public string PdfPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current processing status.
    /// </summary>
    public BulkProcessingStatus Status { get; set; } = BulkProcessingStatus.Pending;

    /// <summary>
    /// Gets or sets the processing result after completion.
    /// </summary>
    public BulkProcessingResult? Result { get; set; }

    /// <summary>
    /// Gets or sets the error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}