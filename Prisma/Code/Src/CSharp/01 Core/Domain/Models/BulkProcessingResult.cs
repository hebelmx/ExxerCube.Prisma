using ExxerCube.Prisma.Domain.Entities;

namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents the complete result of processing a single document through XML, OCR, and comparison.
/// </summary>
public class BulkProcessingResult
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expediente extracted from XML.
    /// </summary>
    public Expediente? XmlExpediente { get; set; }

    /// <summary>
    /// Gets or sets the expediente extracted from OCR.
    /// </summary>
    public Expediente? OcrExpediente { get; set; }

    /// <summary>
    /// Gets or sets the comparison result between XML and OCR.
    /// </summary>
    public ComparisonResult? Comparison { get; set; }

    /// <summary>
    /// Gets or sets whether the processing succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the OCR confidence (from Tesseract).
    /// </summary>
    public float? OcrConfidence { get; set; }

    /// <summary>
    /// Gets or sets the raw OCR text captured during processing.
    /// </summary>
    public string? RawOcrText { get; set; }

    /// <summary>
    /// Gets or sets the sanitized account value (raw + cleaned + warnings).
    /// </summary>
    public TextCleaningResult? AccountSanitization { get; set; }

    /// <summary>
    /// Gets or sets the sanitized SWIFT/BIC value (raw + cleaned + warnings).
    /// </summary>
    public TextCleaningResult? SwiftSanitization { get; set; }

    /// <summary>
    /// Gets or sets the processing duration in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; set; }
}
