using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;

/// <summary>
/// Shared, stage-agnostic value-parsing primitives for VEC statement text tokens.
/// </summary>
/// <remarks>
/// <para>
/// Extracted out of <see cref="PdfPigStatementFieldExtractor"/> so the positional (stage-1)
/// extractor and any future progressive-fallback stage (e.g. a fuzzy-match stage, VERIQAN-E2)
/// share a single source of parsing truth instead of duplicating the same regex/lookup logic.
/// This type is <see langword="internal"/> — visible within this assembly and to
/// <c>ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests</c> via
/// <c>InternalsVisibleTo</c> (see <c>Properties/AssemblyInfo.cs</c>).
/// </para>
/// <para>
/// This is currently limited to the Spanish-date family. Amount/percent parsing remains on
/// <see cref="PdfPigStatementFieldExtractor"/> for now and is expected to move here in a later
/// chunk (VERIQAN-E2.3) — keep that migration separate to keep each diff reviewable.
/// </para>
/// </remarks>
internal static class StatementValueParsers
{
    /// <summary>
    /// Spanish month abbreviation → 1-based month number map.
    /// </summary>
    private static readonly Dictionary<string, int> SpanishMonthMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ene"] = 1, ["enero"] = 1,
            ["feb"] = 2, ["febrero"] = 2,
            ["mar"] = 3, ["marzo"] = 3,
            ["abr"] = 4, ["abril"] = 4,
            ["may"] = 5, ["mayo"] = 5,
            ["jun"] = 6, ["junio"] = 6,
            ["jul"] = 7, ["julio"] = 7,
            ["ago"] = 8, ["agosto"] = 8,
            ["sep"] = 9, ["septiembre"] = 9, ["sept"] = 9,
            ["oct"] = 10, ["octubre"] = 10,
            ["nov"] = 11, ["noviembre"] = 11,
            ["dic"] = 12, ["diciembre"] = 12,
        };

    /// <summary>"d-mmm-yyyy" date format (e.g. "5-jul-2025", "04-ago-2025").</summary>
    private static readonly Regex DateDashFormat = new(
        @"^(\d{1,2})-([a-záéíóúñü]+)-(\d{4})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Attempts to repair a truncated date token where the year is missing its last digit,
    /// e.g. "07-jul-202" → "07-jul-2025" when <paramref name="contextYear"/> is 2025.
    /// </summary>
    /// <param name="raw">Raw date token that may be truncated.</param>
    /// <param name="contextYear">Year to use for repair (from operation date or current year).</param>
    /// <returns>Repaired token, or <see langword="null"/> if repair cannot be applied.</returns>
    internal static string? RepairTruncatedDate(string raw, int contextYear)
    {
        // Pattern: "dd-mmm-YYY" — 3-digit year (missing last digit).
        var m = Regex.Match(raw, @"^(\d{1,2}-[a-záéíóúñü]+-\d{3})$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            return null;

        // Append the last digit of the context year.
        var lastDigit = (contextYear % 10).ToString(CultureInfo.InvariantCulture);
        return raw + lastDigit;
    }

    /// <summary>
    /// Parses Spanish-format dates:
    /// <list type="bullet">
    ///   <item><description>"d-mmm-yyyy" / "dd-mmm-yyyy": e.g. "5-jul-2025"</description></item>
    ///   <item><description>"dd de mmm yyyy": e.g. "04 de ago 2025"</description></item>
    /// </list>
    /// Day-name prefixes ("lunes,") must be stripped by callers.
    /// </summary>
    /// <param name="raw">Raw date token to parse.</param>
    /// <param name="result">The parsed date when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="raw"/> matched a known format.</returns>
    internal static bool TryParseSpanishDate(string? raw, out DateOnly result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();

        // Format 1: "d-mmm-yyyy"
        var m = DateDashFormat.Match(s);
        if (m.Success && TryGetMonth(m.Groups[2].Value, out var mo1))
        {
            if (int.TryParse(m.Groups[1].Value, out var d1)
                && int.TryParse(m.Groups[3].Value, out var y1))
            {
                result = new DateOnly(y1, mo1, d1);
                return true;
            }
        }

        // Format 2: "dd de mmm yyyy" (must have exactly 4 tokens)
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4
            && string.Equals(parts[1], "de", StringComparison.OrdinalIgnoreCase)
            && TryGetMonth(parts[2], out var mo2)
            && int.TryParse(parts[0], out var d2)
            && int.TryParse(parts[3], out var y2))
        {
            result = new DateOnly(y2, mo2, d2);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Looks up a Spanish month abbreviation or full name (case-insensitive) to its 1-based
    /// month number.
    /// </summary>
    /// <param name="abbr">Month abbreviation or full name (e.g. "ago", "agosto").</param>
    /// <param name="month">The resolved 1-based month number when found.</param>
    /// <returns><see langword="true"/> when <paramref name="abbr"/> is a known Spanish month token.</returns>
    internal static bool TryGetMonth(string abbr, out int month)
        => SpanishMonthMap.TryGetValue(abbr, out month);
}
