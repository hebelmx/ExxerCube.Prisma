namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Per-page structural metadata extracted from a PDF page (Story 5.3).
/// </summary>
/// <param name="PageNumber">1-based page index.</param>
/// <param name="HasContent">true if the page contains any words (not blank textually).</param>
/// <param name="ImageCount">Number of embedded images on the page (proxy for logo presence).</param>
/// <param name="ContainsCardNumber">true if page text contains the statement card number (digits-only match).</param>
/// <param name="PaginationCurrent">Current page number parsed from "N de M" footer pattern; null if not found.</param>
/// <param name="PaginationTotal">Total page count parsed from "N de M" footer pattern; null if not found.</param>
/// <param name="Locator">Source location metadata (page-level hint).</param>
public sealed record PageInspectionFacts(
    int PageNumber,
    bool HasContent,
    int ImageCount,
    bool ContainsCardNumber,
    int? PaginationCurrent,
    int? PaginationTotal,
    FieldLocator Locator);
