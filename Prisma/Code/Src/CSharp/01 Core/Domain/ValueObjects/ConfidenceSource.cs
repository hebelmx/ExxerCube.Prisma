namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Identifies the pipeline stage that produced a <see cref="Confidence"/> value,
/// enabling provenance tracking across the full processing chain.
/// </summary>
public enum ConfidenceSource
{
    /// <summary>
    /// Confidence derived from the image quality analysis stage
    /// (e.g. <c>PolynomialImageQualityAnalyzer</c>).
    /// </summary>
    Quality,

    /// <summary>
    /// Confidence reported directly by the OCR engine
    /// (e.g. Tesseract word/block confidence on the 0–100 scale).
    /// </summary>
    Ocr,

    /// <summary>
    /// Confidence assigned during multi-source fusion and reconciliation
    /// (e.g. <c>FusionExpedienteService</c>).
    /// </summary>
    Fusion,

    /// <summary>
    /// Confidence produced by the document classification stage
    /// (e.g. an integer classifier score on the 0–100 scale).
    /// </summary>
    Classification
}
