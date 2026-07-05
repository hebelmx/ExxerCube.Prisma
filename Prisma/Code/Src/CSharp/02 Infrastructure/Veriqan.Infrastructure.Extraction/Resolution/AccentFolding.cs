using System;
using System.Globalization;
using System.Text;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Small Unicode-normalization helper used to compare PDF-extracted label text against a
/// pre-folded alias dictionary independently of accents and casing (see
/// <see cref="FuzzyLabelStage{TValue}"/>).
/// </summary>
/// <remarks>
/// Same NFD-decompose-then-strip-non-spacing-marks technique already used elsewhere in the
/// codebase (e.g. <c>StatementModel.NormalizedFullText</c>, <c>MexicanNameFuzzyMatcher</c>) —
/// duplicated here (rather than shared) because those live in different assemblies and this
/// helper is intentionally tiny.
/// </remarks>
internal static class AccentFolding
{
    /// <summary>
    /// Lowercases <paramref name="text"/> (invariant culture) and removes every Unicode
    /// non-spacing combining mark, so e.g. "Límite" and "limite" compare equal.
    /// </summary>
    /// <param name="text">The raw text to fold.</param>
    /// <returns>The accent-folded, lowercased text.</returns>
    internal static string FoldAndLower(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var normalized = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
