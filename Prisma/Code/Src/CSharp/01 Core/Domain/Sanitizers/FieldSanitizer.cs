// <copyright file="FieldSanitizer.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Sanitizers;

using System.Linq;

/// <summary>
/// Static utility class for sanitizing XML data quality issues before fusion.
/// Implements defensive programming: NEVER crashes, handles ANY input gracefully.
/// </summary>
/// <remarks>
/// <para><strong>Reality of Chaos:</strong></para>
/// <para>
/// The law (R29 A-2911) says "no nulls allowed", but reality is chaotic:
/// </para>
/// <list type="bullet">
/// <item>14 fields don't even come in XML currently (missing entirely)</item>
/// <item>Human annotations everywhere: "NO SE CUENTA", "el monto mencionado en el texto"</item>
/// <item>HTML entities: &amp;nbsp; instead of null</item>
/// <item>Typos and uncontrolled vocabularies: "CUATO MIL" instead of "CUATRO MIL"</item>
/// <item>Trailing whitespace, line breaks, collapsed text</item>
/// <item>Empty RFC fields: 13 spaces instead of null</item>
/// <item>Duplicate persons with different RFCs (same person, variant RFC)</item>
/// <item>Structured data buried in text fields</item>
/// </list>
/// <para><strong>Sanitizer Contract (NEVER CRASH):</strong></para>
/// <list type="number">
/// <item>Given ANY input (null, garbage, malformed), NEVER throw exceptions</item>
/// <item>Return null for unparseable or semantically empty data</item>
/// <item>Return cleaned string for valid data</item>
/// <item>Be idempotent: Sanitize(Sanitize(x)) == Sanitize(x)</item>
/// </list>
/// <para><strong>Data Quality Issues Handled:</strong></para>
/// <list type="number">
/// <item>Trailing/leading whitespace: Trim()</item>
/// <item>HTML entities: &amp;nbsp;, &amp;amp;nbsp; removed</item>
/// <item>Human annotations: "NO SE CUENTA", "NO APLICA", etc. treated as null</item>
/// <item>Line breaks: Replaced with single space</item>
/// <item>Multiple spaces: Collapsed to single space</item>
/// <item>All spaces/underscores: Treated as null (empty RFC field pattern)</item>
/// <item>Currency symbols in amounts: $, MXN, USD removed</item>
/// <item>Thousands separators: Commas removed from amounts</item>
/// <item>Decimal amounts: Rounded to nearest peso (R29 requirement)</item>
/// </list>
/// <para><strong>Usage in Fusion:</strong></para>
/// <para>
/// Sanitization happens BEFORE pattern validation:
/// 1. Extract raw value from XML/PDF/DOCX
/// 2. Sanitize (clean data quality issues)
/// 3. Validate pattern (IsValidRFC, IsValidCURP, etc.)
/// 4. Fuse (if validation passes)
/// </para>
/// </remarks>
public static class FieldSanitizer
{
    /// <summary>
    /// Human annotations detected in XML that should be treated as null.
    /// Case-insensitive matching.
    /// </summary>
    private static readonly HashSet<string> HumanAnnotations = new(StringComparer.OrdinalIgnoreCase)
    {
        "NO SE CUENTA",
        "el monto mencionado en el texto",
        "Se trata de la misma persona con variante en el RFC",
        "NO APLICA",
        "N/A",
        "NA",
        "NO DISPONIBLE",
        "SIN DATO",
        "PENDIENTE",
    };

    /// <summary>
    /// Sanitizes a generic text field by removing data quality issues.
    /// </summary>
    /// <param name="value">Raw field value from XML/PDF/DOCX (may be null, empty, or malformed).</param>
    /// <returns>
    /// Cleaned string if valid data exists, null if semantically empty.
    /// NEVER throws exceptions - handles ANY input gracefully.
    /// </returns>
    /// <remarks>
    /// <para><strong>Sanitization Steps:</strong></para>
    /// <list type="number">
    /// <item>Return null if input is null/empty/whitespace</item>
    /// <item>Trim leading/trailing whitespace</item>
    /// <item>Remove HTML entities (&amp;nbsp;, &amp;amp;nbsp;, &amp;lt;, &amp;gt;)</item>
    /// <item>Replace line breaks (CR, LF, CRLF) with single space</item>
    /// <item>Collapse multiple consecutive spaces to single space</item>
    /// <item>Detect human annotations and return null</item>
    /// <item>Detect all-spaces or all-underscores and return null</item>
    /// <item>Final trim and whitespace check</item>
    /// </list>
    /// <para><strong>Examples:</strong></para>
    /// <code>
    /// Sanitize("  SUBDELEGACION&amp;nbsp;8\r\nSAN    ANGEL   ")
    ///   → "SUBDELEGACION 8 SAN ANGEL"
    ///
    /// Sanitize("NO SE CUENTA")
    ///   → null
    ///
    /// Sanitize("             ") // 13 spaces (empty RFC field)
    ///   → null
    ///
    /// Sanitize(null)
    ///   → null (no crash)
    /// </code>
    /// </remarks>
    public static string? Sanitize(string? value)
    {
        // Step 1: Null/empty/whitespace check (NEVER CRASH)
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Step 2: Trim whitespace
        var cleaned = value.Trim();

        // Step 3: Replace HTML entities with spaces
        cleaned = cleaned.Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("&amp;nbsp;", " ", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("&lt;", " ", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("&gt;", " ", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("&amp;", " ", StringComparison.OrdinalIgnoreCase);

        // Step 4: Replace line breaks with spaces
        cleaned = cleaned.Replace("\r\n", " ", StringComparison.Ordinal);
        cleaned = cleaned.Replace("\n", " ", StringComparison.Ordinal);
        cleaned = cleaned.Replace("\r", " ", StringComparison.Ordinal);

        // Step 5: Collapse multiple spaces to single space
        while (cleaned.Contains("  ", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
        }

        // Step 6: Trim again after replacements
        cleaned = cleaned.Trim();

        // Step 7: Detect human annotations (case-insensitive)
        if (HumanAnnotations.Contains(cleaned))
        {
            return null;
        }

        // Step 8: Detect all-spaces or all-underscores (empty RFC field pattern)
        if (cleaned.All(c => c == ' ' || c == '_'))
        {
            return null;
        }

        // Step 9: Final whitespace check
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        return cleaned;
    }

    /// <summary>
    /// Sanitizes a monetary amount field by removing currency symbols, commas, and rounding to nearest peso.
    /// </summary>
    /// <param name="value">Raw amount value from XML/PDF/DOCX (may include $, commas, decimals).</param>
    /// <returns>
    /// Cleaned amount as string (no decimals, no commas) if valid, null if unparseable.
    /// NEVER throws exceptions - handles ANY input gracefully.
    /// </returns>
    /// <remarks>
    /// <para><strong>R29 A-2911 Amount Requirements:</strong></para>
    /// <list type="bullet">
    /// <item>No decimals (rounded to nearest peso)</item>
    /// <item>No commas or thousands separators</item>
    /// <item>No currency symbols ($, MXN, USD)</item>
    /// <item>Positive values only (zero is valid for "toda la cuenta")</item>
    /// <item>Rounding rule: 0.5 and above rounds up (MidpointRounding.AwayFromZero)</item>
    /// </list>
    /// <para><strong>Examples:</strong></para>
    /// <code>
    /// SanitizeMonto("$236,569.68")
    ///   → "236570" (0.68 rounds up)
    ///
    /// SanitizeMonto("236,569.20")
    ///   → "236569" (0.20 rounds down)
    ///
    /// SanitizeMonto("236570 MXN")
    ///   → "236570"
    ///
    /// SanitizeMonto("0")
    ///   → "0" (toda la cuenta)
    ///
    /// SanitizeMonto("-100")
    ///   → null (negative invalid)
    ///
    /// SanitizeMonto("INVALID")
    ///   → null (no crash)
    /// </code>
    /// </remarks>
    public static string? SanitizeMonto(string? value)
    {
        // Step 1: Use generic sanitizer first
        var cleaned = Sanitize(value);
        if (cleaned == null)
        {
            return null;
        }

        // Step 2: Remove currency symbols
        cleaned = cleaned.Replace("$", string.Empty, StringComparison.Ordinal);
        cleaned = cleaned.Replace("MXN", string.Empty, StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("USD", string.Empty, StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("EUR", string.Empty, StringComparison.OrdinalIgnoreCase);

        // Step 3: Remove thousands separators
        cleaned = cleaned.Replace(",", string.Empty, StringComparison.Ordinal);
        cleaned = cleaned.Replace(" ", string.Empty, StringComparison.Ordinal);

        // Step 4: Trim again
        cleaned = cleaned.Trim();

        // Step 5: Try to parse as decimal (NEVER CRASH)
        if (!decimal.TryParse(cleaned, out var amount))
        {
            return null;
        }

        // Step 6: Validate positive (zero is valid for "toda la cuenta")
        if (amount < 0)
        {
            return null;
        }

        // Step 7: Round to nearest peso (R29 requirement: >0.5 rounds up)
        var rounded = Math.Round(amount, 0, MidpointRounding.AwayFromZero);

        // Step 8: Return as string with no decimals
        return rounded.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
