using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// Shared text-normalization helper for legend-presence rules (CL-32, CL-46).
/// </summary>
/// <remarks>
/// <para>
/// The normalization contract must be identical to the one applied by
/// <c>PdfPigStatementFieldExtractor.NormalizeText</c> in the Extraction assembly so that
/// search strings match the stored <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/>.
/// Both assemblies own their own copy of this algorithm to avoid an infra-to-infra project
/// reference.  If the algorithm changes, update both copies together.
/// </para>
/// <para>
/// Normalization steps:
/// <list type="number">
///   <item>Upper-case (<see cref="string.ToUpperInvariant"/>).</item>
///   <item>Accent strip: NFD decomposition + removal of <see cref="UnicodeCategory.NonSpacingMark"/>.</item>
///   <item>Collapse whitespace runs to a single ASCII space; trim.</item>
/// </list>
/// </para>
/// </remarks>
internal static class TextNormalizer
{
    private static readonly Regex WhitespaceCollapsePattern =
        new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Normalizes a text string for legend-presence matching.
    /// </summary>
    /// <param name="text">Raw text to normalize. May be <see langword="null"/> or empty.</param>
    /// <returns>
    /// Normalized string, or <see cref="string.Empty"/> when the input is <see langword="null"/>
    /// or whitespace.
    /// </returns>
    internal static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Step 1: upper-case (invariant to avoid Turkish-I issues).
        var upper = text.ToUpperInvariant();

        // Step 2: strip diacritics via NFD decomposition.
        var nfd = upper.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(nfd.Length);
        foreach (var c in nfd)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        // Step 3: collapse whitespace.
        return WhitespaceCollapsePattern.Replace(sb.ToString(), " ").Trim();
    }
}
