namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

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
/// <b>Conditional sections (§16, §23, §25):</b> these are only required when their
/// trigger condition is present (e.g. other credit lines, unrecognized charges, debt
/// restructuring).  When the trigger is absent, <see cref="IsApplicable"/> is set to
/// <see langword="false"/> and the section is NOT counted as missing by validation rules —
/// abstaining rather than risking a false Fail.
/// </para>
/// <para>
/// <b>Locator:</b> when a section is found, <see cref="Locator"/> contains the page
/// number and bounding box of the heading band.  When the section is absent,
/// <see cref="Locator"/> is <see cref="FieldLocator.NoPage"/> (page 0).
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
/// <see langword="true"/> when the section heading anchor was found in the PDF text layer.
/// </param>
/// <param name="IsApplicable">
/// <see langword="true"/> for all unconditional sections.
/// <see langword="false"/> for conditional sections (§16, §23, §25) whose trigger
/// condition was not detected in the document.  A section that is not applicable is
/// never counted as missing by <c>MandatorySectionsPresenceRule</c>.
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
    FieldLocator Locator);
