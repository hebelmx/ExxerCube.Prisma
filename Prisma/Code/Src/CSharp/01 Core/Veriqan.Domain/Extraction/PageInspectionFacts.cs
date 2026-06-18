namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Per-page structural metadata extracted from a PDF page (Story 5.3, extended Story 10.1).
/// </summary>
/// <param name="PageNumber">1-based page index.</param>
/// <param name="HasContent">true if the page contains any words (not blank textually).</param>
/// <param name="ImageCount">Number of embedded images on the page (proxy for logo presence).</param>
/// <param name="ContainsCardNumber">true if page text contains the statement card number (digits-only match).</param>
/// <param name="PaginationCurrent">Current page number parsed from "N de M" footer pattern; null if not found.</param>
/// <param name="PaginationTotal">Total page count parsed from "N de M" footer pattern; null if not found.</param>
/// <param name="Locator">Source location metadata (page-level hint).</param>
/// <param name="Width">
/// Page width in PDF points as reported by PdfPig's <c>page.Width</c> (Story 10.1).
/// Zero when the page could not be read.
/// Story 10.2 uses this to compute the 2 cm blank-gap threshold in the same unit system.
/// </param>
/// <param name="Height">
/// Page height in PDF points as reported by PdfPig's <c>page.Height</c> (Story 10.1).
/// Zero when the page could not be read.
/// Story 10.2 uses this to convert the 2 cm gap to PDF points: <c>gap_pt = (2/2.54) × 72</c>.
/// </param>
public sealed record PageInspectionFacts(
    int PageNumber,
    bool HasContent,
    int ImageCount,
    bool ContainsCardNumber,
    int? PaginationCurrent,
    int? PaginationTotal,
    FieldLocator Locator,
    double Width = 0.0,
    double Height = 0.0);
