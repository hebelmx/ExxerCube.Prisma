namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// The reliability of a section-presence determination.
/// </summary>
/// <remarks>
/// Used by downstream validation rules to distinguish between a confident absence
/// (the heading band was scanned and the anchor was not found) and an indeterminate
/// result (the section cannot be reliably detected from the text layer — e.g. §1 Logo
/// is a visual image element with no text anchor).
/// </remarks>
public enum SectionDetectionStatus
{
    /// <summary>
    /// The section heading anchor was found in a heading band.
    /// <see cref="DetectedSection.IsPresent"/> is <see langword="true"/>.
    /// </summary>
    Present,

    /// <summary>
    /// All heading bands were scanned and the anchor was not found.
    /// <see cref="DetectedSection.IsPresent"/> is <see langword="false"/>.
    /// </summary>
    Absent,

    /// <summary>
    /// The section cannot be reliably determined from the PDF text layer.
    /// No heading-band anchor exists (e.g. §1 Logo del Banco is a visual image).
    /// Validation rules MUST abstain (not Fail) for indeterminate sections.
    /// <see cref="DetectedSection.IsPresent"/> is <see langword="false"/> for
    /// backward compatibility, but callers should test
    /// <see cref="DetectedSection.DetectionStatus"/> == <see cref="Indeterminate"/>
    /// rather than relying on <see cref="DetectedSection.IsPresent"/> alone.
    /// </summary>
    Indeterminate,
}

/// <summary>
/// How a <see cref="DetectedSection"/>'s presence was determined.
/// </summary>
/// <remarks>
/// Introduced by the §-anchor OCR escalation ladder (RC1.S6): on real-world statements where
/// every section heading is raster/image-rendered, the PDF text layer alone cannot find any
/// heading band. When the text-layer pass locates fewer than 2 present sections, an escalation
/// stage renders each page and runs OCR, re-scanning the recognized text for the same anchor
/// phrases. A section upgraded this way carries <see cref="Ocr"/> so downstream rules — and any
/// future geometry-dependent logic — can tell the difference between a text-layer-measured
/// heading (with real PDF-point bounding-box geometry) and an OCR-recognized one (page number
/// only, no bounding box: <see cref="FieldLocator.PageHint"/>).
/// </remarks>
public enum SectionDetectionSource
{
    /// <summary>Detected (or found absent) by scanning the PDF text layer's word bands. Default —
    /// preserves the pre-escalation behavior for every existing caller/test.</summary>
    TextLayer,

    /// <summary>
    /// Detected by the §-anchor OCR escalation stage: the text layer found this heading's anchor
    /// nowhere, a rendered-page OCR pass did. <see cref="DetectedSection.Locator"/> carries only a
    /// page number (<see cref="FieldLocator.PageHint"/>) — never a fabricated bounding box —
    /// because OCR text has no PDF-point word geometry.
    /// </summary>
    Ocr,
}

/// <summary>
/// Represents one of the 28 mandatory CONDUSEF <i>Acuerdo</i> sections detected (or not found)
/// in a VEC credit-card statement (Story 10.1).
/// </summary>
/// <remarks>
/// <para>
/// Each section is identified by its <see cref="SectionNumber"/> (1–28) and a stable,
/// normalized anchor phrase derived from the section's mandated content in the DOF Acuerdo
/// (mandatory since 17-Oct-2024).  Anchor matching uses <see cref="VecTextNormalizer.Normalize"/>
/// so comparisons are accent- and case-insensitive.
/// </para>
/// <para>
/// <b>Presence is heading-band-only (Story 10.1 R1):</b> a section is considered present only
/// when its anchor phrase is found in a per-page word band (the heading-locator scan).  A bare
/// occurrence of the anchor in body text or the Glosario de Términos (§27) does NOT mark the
/// section present.
/// </para>
/// <para>
/// <b>Conditional sections (§16, §23, §25) and optional sections (§21, §28):</b> when absent,
/// <see cref="IsApplicable"/> is set to <see langword="false"/> and the section is NOT counted
/// as missing by validation rules — abstaining rather than risking a false Fail.
/// </para>
/// <para>
/// <b>Indeterminate sections (§1 Logo):</b> the logo is a visual image element with no text
/// anchor; <see cref="DetectionStatus"/> is set to <see cref="SectionDetectionStatus.Indeterminate"/>
/// and <see cref="IsApplicable"/> is <see langword="false"/> so the rule abstains.
/// </para>
/// <para>
/// <b>Locator:</b> when a section is found, <see cref="Locator"/> contains the page
/// number and bounding box of the heading band.  When the section is absent,
/// <see cref="Locator"/> is <see cref="FieldLocator.NoPage"/> (page 0).
/// </para>
/// <para>
/// <b>SectionText (additive — Story 10.1 R1):</b> the normalized text from this section's
/// heading down to the next detected section heading in reading order (or end of document).
/// Empty string when the section is absent or indeterminate.  Used by R2 rules to scope
/// verbatim-match checks within the correct section instead of the whole document.
/// </para>
/// </remarks>
/// <param name="SectionNumber">
/// The Acuerdo section numeral (1–28) as defined in the DOF CONDUSEF Acuerdo
/// <i>formato de estado de cuenta estandarizado de tarjeta de crédito</i>.
/// </param>
/// <param name="Name">
/// Canonical Spanish section title (stable identifier used for display and logging).
/// </param>
/// <param name="IsPresent">
/// <see langword="true"/> when the section heading anchor was found in a heading band of the PDF text layer.
/// <see langword="false"/> for absent or indeterminate sections.
/// For indeterminate sections check <see cref="DetectionStatus"/> == <see cref="SectionDetectionStatus.Indeterminate"/>.
/// </param>
/// <param name="IsApplicable">
/// <see langword="true"/> for all unconditional sections that are present or absent with a confident result.
/// <see langword="false"/> for:
/// <list type="bullet">
///   <item>Conditional sections (§16, §23, §25) whose trigger condition was not detected.</item>
///   <item>Optional sections (§21, §28) that are legitimately absent.</item>
///   <item>Indeterminate sections (§1) where detection is not possible from the text layer.</item>
/// </list>
/// A section that is not applicable is never counted as missing by <c>MandatorySectionsPresenceRule</c>.
/// </param>
/// <param name="Locator">
/// Source location of the section heading in the PDF.
/// <see cref="FieldLocator.NoPage"/> when the section was not found.
/// </param>
public sealed record DetectedSection(
    int SectionNumber,
    string Name,
    bool IsPresent,
    bool IsApplicable,
    FieldLocator Locator)
{
    // -----------------------------------------------------------------------
    // Additive members (Story 10.1 R1) — defaults preserve backward compatibility
    // -----------------------------------------------------------------------

    /// <summary>
    /// The reliability of this section's presence determination (Story 10.1 R1).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><see cref="SectionDetectionStatus.Present"/> — heading anchor found in a band.</item>
    ///   <item><see cref="SectionDetectionStatus.Absent"/> — all bands scanned, anchor not found.</item>
    ///   <item><see cref="SectionDetectionStatus.Indeterminate"/> — section has no text anchor
    ///     (e.g. §1 Logo del Banco is a visual image); validation rules must abstain, not Fail.</item>
    /// </list>
    /// Default is <see cref="SectionDetectionStatus.Absent"/> so that records constructed without
    /// this parameter (pre-R1 code) behave as before.
    /// </remarks>
    public SectionDetectionStatus DetectionStatus { get; init; } = SectionDetectionStatus.Absent;

    /// <summary>
    /// Normalized text from this section's heading down to the next detected section's heading
    /// in document reading order (Story 10.1 R1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Populated for present sections only.  Empty string when the section is absent or
    /// indeterminate.  Also empty when no words were found between this section's heading
    /// and the next section boundary.
    /// </para>
    /// <para>
    /// Normalization matches <see cref="VecTextNormalizer.Normalize"/>:
    /// upper-cased, accent-stripped, whitespace collapsed.
    /// </para>
    /// <para>
    /// Downstream rules (R2) should use this property to scope verbatim-match checks within the
    /// correct section instead of scanning the whole document's <c>NormalizedFullText</c>.
    /// </para>
    /// </remarks>
    public string SectionText { get; init; } = string.Empty;

    /// <summary>
    /// How this section's presence/absence was determined (RC1.S6 — §-anchor OCR escalation
    /// ladder). Defaults to <see cref="SectionDetectionSource.TextLayer"/> so every record
    /// constructed before this property existed (and every call site that never sets it) keeps
    /// its original, unescalated meaning.
    /// </summary>
    public SectionDetectionSource Source { get; init; } = SectionDetectionSource.TextLayer;
}
