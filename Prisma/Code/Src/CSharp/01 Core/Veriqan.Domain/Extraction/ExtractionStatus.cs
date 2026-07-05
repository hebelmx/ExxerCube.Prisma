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

    // TODO(E3/E5): decide coverage-count semantics when a stage first emits ExtractedByInference.

    /// <summary>
    /// The value was resolved by a semantic-search or LLM inference stage rather than read
    /// positionally. Treated as a candidate that a downstream consumer MAY require human
    /// confirmation for before it gates a verdict (mirrors Prisma's <c>RequiresManualReview</c>
    /// pattern). No stage emits this yet as of E1.
    /// </summary>
    ExtractedByInference,
}
