namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>Outcome of extracting a financial regulatory table from a VEC PDF.</summary>
public enum TableExtractionStatus
{
    /// <summary>Table reconstructed with at least one row.</summary>
    Extracted,
    /// <summary>Section heading was found but no rows could be reconstructed.</summary>
    NoRowsParsed,
    /// <summary>Section heading was not found in the document.</summary>
    SectionNotFound,
    /// <summary>
    /// Table could not be reliably reconstructed (e.g. two-column layout bleed,
    /// insufficient geometry data, mixed NA/blank cells).
    /// Validation rules MUST abstain (InsufficientData), never Fail.
    /// </summary>
    Indeterminate,
}
