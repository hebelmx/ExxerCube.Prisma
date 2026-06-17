namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Records a text-overlap incident found during PDF extraction (Story 5.2 — CL-28).
/// An incident represents two words on the same horizontal band whose X-extents
/// intersect by more than the extraction epsilon (2.0 PDF points), indicating
/// layout-level overlap rather than normal kerning or glyph touching.
/// </summary>
/// <param name="PageNumber">
/// 1-based page number on which the overlapping words were found.
/// </param>
/// <param name="OverlapPoints">
/// The X-axis intersection magnitude in PDF points.
/// Always greater than the extraction epsilon (2.0 pt); never negative.
/// </param>
/// <param name="Locator">
/// Bounding box that spans both overlapping words on the page.
/// </param>
/// <param name="SampleText">
/// The combined text of the two overlapping words, formatted as
/// <c>"'word1' ∩ 'word2'"</c> for diagnostic display.
/// </param>
public sealed record TextOverlapIncident(
    int PageNumber,
    double OverlapPoints,
    FieldLocator Locator,
    string SampleText);
