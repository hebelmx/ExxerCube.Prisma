using System.Text.RegularExpressions;

namespace ExxerCube.Prisma.Application.Parsing;

/// <summary>
/// Parses identity-related fields (RFC variants, CURP) from raw text.
/// </summary>
public static class IdentityParser
{
    private static readonly Regex RfcRegex = new(@"\b[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CurpRegex = new(@"\b[A-Z][AEIOUX][A-Z]{2}\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])[HM][A-Z]{2}[B-DF-HJ-NP-TV-Z]{3}[A-Z\d]\d\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extracts RFC variants from raw text (OCR/XML/DOCX), uppercased and deduplicated.
    /// </summary>
    public static IEnumerable<string> ParseRfcVariants(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Enumerable.Empty<string>();
        }

        return RfcRegex.Matches(raw)
            .Select(m => m.Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts a CURP value from raw text, uppercased if present.
    /// </summary>
    public static string? ParseCurp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var match = CurpRegex.Match(raw);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }
}
