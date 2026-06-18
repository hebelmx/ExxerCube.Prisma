namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Represents a measured vertical whitespace gap between two consecutive detected sections
/// on the same PDF page (Story 10.2).
/// </summary>
/// <remarks>
/// <para>
/// Coordinates are in PDF points (origin bottom-left, as used by PdfPig).
/// The gap is measured as the largest empty vertical band (no word bounding boxes) between
/// the bottom edge of the last content line in section <see cref="AfterSectionNumber"/> and
/// the bottom edge of the heading of <see cref="BeforeSectionNumber"/> on the same page.
/// </para>
/// <para>
/// Gaps that span a page break are <b>not</b> recorded here — natural end-of-page and
/// top-of-next-page whitespace is excluded by design (it would always exceed 2 cm and would
/// produce systematic false-Fail findings).  Only same-page inter-section gaps are tracked.
/// </para>
/// <para>
/// The rule threshold is: 2 cm = 2 / 2.54 × 72 pt ≈ 56.69 pt.
/// The constant <c>GapThresholdPoints</c> lives in <see cref="SectionGap.TwoCmInPoints"/>
/// for reference; the canonical value used by the rule is in <c>SectionOrderAndGapRule</c>.
/// </para>
/// </remarks>
/// <param name="AfterSectionNumber">
/// The <see cref="DetectedSection.SectionNumber"/> (1–28) immediately <em>before</em> the gap
/// (i.e. the section whose content ends above the gap).
/// </param>
/// <param name="BeforeSectionNumber">
/// The <see cref="DetectedSection.SectionNumber"/> (1–28) immediately <em>after</em> the gap
/// (i.e. the section whose heading starts below the gap).
/// </param>
/// <param name="PageNumber">
/// 1-based page number on which the gap was measured.
/// </param>
/// <param name="GapPoints">
/// Measured gap height in PDF points (always ≥ 0).
/// A value of zero means the two sections are adjacent with no whitespace.
/// </param>
public sealed record SectionGap(
    int AfterSectionNumber,
    int BeforeSectionNumber,
    int PageNumber,
    double GapPoints)
{
    /// <summary>
    /// Conversion factor: 1 cm = 28.3464567 PDF points (1 pt = 1/72 in; 1 in = 2.54 cm).
    /// </summary>
    public const double CmToPoints = 28.3464567;

    /// <summary>
    /// The 2 cm threshold expressed in PDF points: 2 × 28.3464567 ≈ 56.6929 pt.
    /// </summary>
    public const double TwoCmInPoints = 2.0 * CmToPoints;
}
