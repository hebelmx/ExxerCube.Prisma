namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Indicates the outcome of the font-run extraction pass (Story 5.1 — CL-35).
/// </summary>
public enum FontExtractionStatus
{
    /// <summary>
    /// The PDF text layer was scanned and at least one letter was found;
    /// <see cref="StatementModel.FontRuns"/> is populated.
    /// </summary>
    Extracted,

    /// <summary>
    /// No letters were found in the PDF text layer (scanned PDF or empty document);
    /// <see cref="StatementModel.FontRuns"/> is empty.
    /// </summary>
    NotFound,
}
