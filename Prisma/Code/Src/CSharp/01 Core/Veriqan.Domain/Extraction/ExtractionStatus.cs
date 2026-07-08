namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Describes the outcome of extracting a single field from a statement PDF.
/// </summary>
public enum ExtractionStatus
{
    /// <summary>
    /// The field was found and its value passes all applicable format/validation rules.
    /// </summary>
    Extracted,

    /// <summary>
    /// The field label was not found in the document, or the value region was empty.
    /// The corresponding <see cref="ExtractedField{T}"/> still carries a best-effort
    /// <see cref="FieldLocator"/> hint and a zero <c>Confidence</c>.
    /// </summary>
    NotExtracted,

    /// <summary>
    /// The field was found but its value violates a known format rule
    /// (e.g. CLABE is not 18 digits, RFC does not match the Mexican pattern).
    /// The raw value is preserved so downstream checks can report it as a defect.
    /// </summary>
    ExtractedInvalidFormat,

    /// <summary>
    /// The value was resolved by a semantic-search, LLM inference, or header-image OCR stage
    /// rather than read positionally from the document's own text layer. Treated as a candidate
    /// that a downstream consumer MAY require human confirmation for before it gates a verdict
    /// (mirrors Prisma's <c>RequiresManualReview</c> pattern). Resolved (E7.S7.2/S7.3, owner
    /// ruling 4): <c>VerificationPipeline.CountExtractedFields</c>'s <c>IsExtracted</c> helper
    /// intentionally does NOT match this status, so a field with this status never counts toward
    /// the extraction-coverage floor — the floor stays a positional-text-layer coverage measure,
    /// not inflated by a second-source inference recovery. First stage to emit this:
    /// <c>StageId.HeaderImageOcr</c> (header-image product-name OCR).
    /// </summary>
    ExtractedByInference,
}
