namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Indicates the outcome of the word-level typography extraction pass (Epic 12).
/// </summary>
public enum TypographyExtractionStatus
{
    /// <summary>
    /// The PDF text layer was scanned and at least one non-whitespace word was found;
    /// <see cref="StatementModel.TypographySamples"/> is populated.
    /// </summary>
    Extracted,

    /// <summary>
    /// No non-whitespace words were found in the PDF text layer (scanned PDF or empty document);
    /// <see cref="StatementModel.TypographySamples"/> is empty.
    /// </summary>
    NotFound,
}
