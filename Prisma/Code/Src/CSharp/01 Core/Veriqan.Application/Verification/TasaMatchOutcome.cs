namespace ExxerCube.Prisma.Veriqan.Application.Verification;

/// <summary>
/// Outcome of matching an extracted TASA against the reference bundle (FR-5 / CL-9).
/// </summary>
public enum TasaMatchOutcome
{
    /// <summary>
    /// The extracted TASA matches the bundle's TASA for the product + period within tolerance.
    /// </summary>
    Matched,

    /// <summary>
    /// The extracted TASA was found in the statement and the bundle entry was found,
    /// but the values differ beyond the allowed tolerance.
    /// </summary>
    Mismatch,

    /// <summary>
    /// The bundle does not contain a TASA entry for the resolved product + period,
    /// so the match cannot be performed.
    /// Checklist checks should emit <c>INSUFFICIENT_REFERENCE_DATA</c> in this case.
    /// </summary>
    InsufficientData,

    /// <summary>
    /// The statement did not yield an extracted TASA value (field is
    /// <see cref="Domain.Extraction.ExtractionStatus.NotExtracted"/>),
    /// so the match cannot be performed.
    /// </summary>
    ExtractedTasaMissing,
}
