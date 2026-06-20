namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Represents a distinct embedded-font run found in the document (Story 5.1 — CL-35).
/// One <see cref="FontUsage"/> instance is produced per unique (normalized-family, page)
/// combination; the <see cref="Locator"/> captures the first occurrence on that page.
/// </summary>
/// <param name="FontName">
/// The raw font name as embedded in the PDF (e.g. <c>"ABCDEE+Aptos-Bold"</c>).
/// May contain a subset prefix (<c>"XXXXXX+"</c>) and a style suffix (<c>"-Bold"</c>).
/// Use the normalization logic in the visual rule to derive the family name.
/// </param>
/// <param name="PageNumber">
/// 1-based page number on which this font was first encountered.
/// </param>
/// <param name="Locator">
/// Bounding-box of the first letter glyph that used this font on this page.
/// </param>
/// <param name="IsEmbedded">
/// <see langword="true"/> when the font program is embedded in the PDF stream;
/// <see langword="false"/> when it is referenced by name only (not embedded) or when
/// the embedding status could not be determined from the extraction layer.
/// </param>
public sealed record FontUsage(
    string FontName,
    int PageNumber,
    FieldLocator Locator,
    bool IsEmbedded = false);
