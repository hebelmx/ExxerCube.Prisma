namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Describes the outcome of extracting the DESGLOSE DE MOVIMIENTOS DEL PERIODO
/// transaction table from a VEC statement PDF (Story 4.4).
/// </summary>
public enum MovementsExtractionStatus
{
    /// <summary>
    /// The DESGLOSE section header was found and at least one movement row was successfully
    /// parsed.  <see cref="StatementModel.Movements"/> is non-empty.
    /// </summary>
    Extracted,

    /// <summary>
    /// The DESGLOSE section header was not found in any page of the PDF.
    /// This is the expected result for short test PDFs (e.g. minimal 1-page PDFs
    /// used in unit tests) that do not contain a transaction table.
    /// <see cref="StatementModel.Movements"/> is empty.
    /// </summary>
    SectionNotFound,

    /// <summary>
    /// The DESGLOSE section header was found but no data rows could be reconstructed
    /// from the page words.  This may indicate an unexpected table layout or an
    /// all-summary-rows page.
    /// <see cref="StatementModel.Movements"/> is empty.
    /// </summary>
    NoRowsParsed,
}
