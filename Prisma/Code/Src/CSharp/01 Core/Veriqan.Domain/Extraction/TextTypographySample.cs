namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Word-level typography sample captured from the PDF text layer (Epic 12).
/// One instance is produced per non-whitespace word across all pages.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PointSize"/> is the rendered point size, computed from the text/CTM matrix by
/// PdfPig — it accounts for scaling, so it reflects the size the reader actually sees on the
/// page, not the nominal font size stored in the font dictionary.  Always use this value when
/// comparing against regulatory point-size floors.
/// </para>
/// <para>
/// <see cref="FontName"/> is the raw embedded name exactly as PdfPig returns it, including
/// any 6-letter+'+' subset prefix (e.g. <c>"ABCDEE+Aptos-Bold"</c>) and any style suffix
/// (e.g. <c>"-Bold"</c>).  The raw name is kept so that downstream rules can judge
/// weight-indeterminacy (font names without a bold indicator require additional heuristics).
/// </para>
/// <para>
/// <see cref="IsBold"/> is a convenience flag derived solely from whether
/// <see cref="FontName"/> contains the word <c>"bold"</c> (OrdinalIgnoreCase).
/// It is not authoritative — rules that require a high-confidence bold determination should
/// also inspect the raw <see cref="FontName"/> and the per-glyph stroke widths.
/// </para>
/// </remarks>
/// <param name="Text">
/// The word's text as reconstructed by PdfPig's built-in word grouper.
/// </param>
/// <param name="PointSize">
/// Rendered point size (CTM-accounted).  Always positive for visible glyphs.
/// </param>
/// <param name="FontName">
/// Raw embedded font name as stored in the PDF (e.g. <c>"ABCDEE+Aptos-Bold"</c>).
/// Retains subset prefix and style suffix — do NOT normalize before storing here.
/// </param>
/// <param name="IsBold">
/// <see langword="true"/> when <see cref="FontName"/> contains <c>"bold"</c>
/// (OrdinalIgnoreCase); <see langword="false"/> otherwise.
/// </param>
/// <param name="PageNumber">
/// 1-based page number on which this word appears.
/// </param>
/// <param name="Locator">
/// Bounding box of the word in PDF points (bottom-left origin, as returned by PdfPig).
/// </param>
public sealed record TextTypographySample(
    string Text,
    double PointSize,
    string FontName,
    bool IsBold,
    int PageNumber,
    FieldLocator Locator);
