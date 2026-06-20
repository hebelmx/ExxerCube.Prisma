namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Per-page structural metadata extracted from a PDF page (Story 5.3, extended Story 10.1/S11).
/// </summary>
/// <param name="PageNumber">1-based page index.</param>
/// <param name="HasContent">
/// <see langword="true"/> if the page contains at least one word whose text is not purely
/// whitespace or empty (Story S11: invisible/whitespace-only glyphs are excluded before this
/// decision, so a page carrying only a whitespace token is correctly reported as blank).
/// </param>
/// <param name="ImageCount">Number of embedded images on the page (proxy for logo presence).</param>
/// <param name="ContainsCardNumber">true if page text contains the statement card number (digits-only match).</param>
/// <param name="PaginationCurrent">Current page number parsed from "N de M" footer pattern; null if not found.</param>
/// <param name="PaginationTotal">Total page count parsed from "N de M" footer pattern; null if not found.</param>
/// <param name="Locator">Source location metadata (page-level hint).</param>
/// <param name="Width">
/// Page width in PDF points as reported by PdfPig's <c>page.Width</c> (Story 10.1).
/// Zero when the page could not be read.
/// </param>
/// <param name="Height">
/// Page height in PDF points as reported by PdfPig's <c>page.Height</c> (Story 10.1).
/// Zero when the page could not be read.
/// </param>
/// <param name="MaxVerticalGapPoints">
/// The largest vertical gap (in PDF points) between consecutive content lines on this page
/// (Story S11 — CL-48 "sin espacio en blanco mayor a 2 cm").
/// A "gap" is the distance between the top of one line-band and the bottom of the next
/// line-band above it, computed only between content lines (top/bottom page margins are
/// excluded).  Zero when the page has fewer than two content lines.
/// The CL-48 threshold is 56.7 pt (2 cm × 28.35 pt/cm); the CL-48 rule
/// applies that threshold.
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
    double Height = 0.0,
    double MaxVerticalGapPoints = 0.0);
