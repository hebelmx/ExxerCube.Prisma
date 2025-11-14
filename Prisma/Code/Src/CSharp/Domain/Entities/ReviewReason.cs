namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents the reason why a case requires manual review.
/// </summary>
public enum ReviewReason
{
    /// <summary>
    /// Low confidence - classification or extraction confidence is below threshold (typically &lt; 80%).
    /// </summary>
    LowConfidence,

    /// <summary>
    /// Ambiguous classification - classification result is ambiguous or unclear.
    /// </summary>
    AmbiguousClassification,

    /// <summary>
    /// Extraction error - field extraction encountered errors or conflicts.
    /// </summary>
    ExtractionError
}

