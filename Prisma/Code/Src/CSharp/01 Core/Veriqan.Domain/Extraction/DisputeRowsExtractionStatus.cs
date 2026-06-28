namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Describes the outcome of extracting §23 <i>Cargos no reconocidos</i> dispute rows
/// from a VEC credit-card statement (Story E10.C4′).
/// </summary>
/// <remarks>
/// Mirrors the three-state design of <see cref="MovementsExtractionStatus"/> to make the
/// abstain-safe guard pattern consistent across all extraction result consumers.
/// </remarks>
public enum DisputeRowsExtractionStatus
{
    /// <summary>
    /// The §23 section was present, applicable, and at least one dispute row with a
    /// parseable amount and status token was extracted.
    /// <see cref="StatementModel.DisputeRows"/> is non-empty.
    /// </summary>
    Extracted,

    /// <summary>
    /// The §23 section was absent or not applicable (trigger condition not detected).
    /// This is the expected result for statements without unrecognized-charge disputes.
    /// <see cref="StatementModel.DisputeRows"/> is empty.
    /// </summary>
    SectionNotFound,

    /// <summary>
    /// The §23 section was present and applicable but no dispute rows could be parsed
    /// from the section text (e.g. layout not recognized by the best-effort extractor,
    /// or section text was empty after normalization).
    /// <see cref="StatementModel.DisputeRows"/> is empty.
    /// </summary>
    NoRowsParsed,
}
