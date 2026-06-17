namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Records the typographic style of a detected section header (Story 5.2 — CL-29).
/// Section headers are identified by matching known Spanish title strings
/// (case-insensitive, normalized) against page text.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bold detection:</b> <see cref="IsBold"/> is <see langword="true"/> when the raw
/// font name of the first letter in the header band contains the string
/// <c>"Bold"</c> (case-insensitive).
/// </para>
/// <para>
/// <b>Uppercase detection:</b> <see cref="IsUppercase"/> is <see langword="true"/>
/// when every alphabetic character in the printed <see cref="HeaderText"/> is uppercase
/// (i.e. the text as it appears in the PDF uses all-caps lettering, which is
/// the expected style for VEC section titles).
/// </para>
/// </remarks>
/// <param name="HeaderText">
/// The verbatim text of the header band as it appears in the PDF
/// (joined from individual word tokens on the same Y-band).
/// </param>
/// <param name="IsBold">
/// <see langword="true"/> when the first letter of the header uses a Bold font variant.
/// </param>
/// <param name="IsUppercase">
/// <see langword="true"/> when all alphabetic characters in <see cref="HeaderText"/>
/// are uppercase.
/// </param>
/// <param name="PageNumber">
/// 1-based page number on which the header was found.
/// </param>
/// <param name="Locator">
/// Bounding box spanning the header text on the page.
/// </param>
public sealed record SectionHeaderStyle(
    string HeaderText,
    bool IsBold,
    bool IsUppercase,
    int PageNumber,
    FieldLocator Locator);
