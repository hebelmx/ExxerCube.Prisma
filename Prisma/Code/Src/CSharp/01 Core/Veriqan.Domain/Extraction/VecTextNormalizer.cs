using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Canonical text-normalization helper shared across the Veriqan assemblies.
/// </summary>
/// <remarks>
/// <para>
/// Both the Validation infrastructure (CL-32, CL-46 legend-presence rules) and the
/// Extraction infrastructure (<see cref="StatementModel.NormalizedFullText"/> construction)
/// must apply exactly the same normalization algorithm so that search strings match
/// stored text.  This single Domain implementation is the single source of truth;
/// both assemblies reference Veriqan.Domain (via Veriqan.Application) and call
/// <see cref="Normalize"/> directly.
/// </para>
/// <para>
/// Normalization steps:
/// <list type="number">
///   <item>Upper-case via <see cref="string.ToUpperInvariant"/> (avoids Turkish-I issues).</item>
///   <item>Accent strip: Unicode NFD decomposition followed by removal of
///         <see cref="UnicodeCategory.NonSpacingMark"/> characters.</item>
///   <item>Collapse all whitespace runs to a single ASCII space; trim leading/trailing
///         space.</item>
/// </list>
/// </para>
/// </remarks>
public static class VecTextNormalizer
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
    public static string Normalize(string? text)
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
