using System.Text;
using System.Text.RegularExpressions;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;

namespace ExxerCube.Prisma.Infrastructure.Extraction;

/// <summary>
/// Default OCR text cleaner that normalizes noisy captures while keeping the raw text and warnings.
/// </summary>
public sealed class TextSanitizer : ITextSanitizer
{
    private static readonly Regex NonDigitRegex = new(@"[^\d]", RegexOptions.Compiled);
    private static readonly Regex NonAlphaNumericRegex = new(@"[^A-Za-z0-9]", RegexOptions.Compiled);
    private const int MinAccountLength = 6;
    private const int MaxAccountLength = 20;

    /// <summary>
    /// Cleans account-number-like text by stripping non-digits and returning raw + cleaned + warnings.
    /// </summary>
    /// <param name="raw">Raw OCR or source text.</param>
    /// <returns>Cleaning result with normalization warnings only (non-blocking).</returns>
    public TextCleaningResult CleanAccount(string? raw)
    {
        var source = raw ?? string.Empty;
        var cleaned = NonDigitRegex.Replace(source, string.Empty);

        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            warnings.Add("AccountMissing");
        }
        else
        {
            if (cleaned.Length < MinAccountLength || cleaned.Length > MaxAccountLength)
            {
                warnings.Add("AccountLengthSuspect");
            }
            if (!string.Equals(cleaned, source, StringComparison.Ordinal))
            {
                warnings.Add("AccountNormalized");
            }
        }

        return new TextCleaningResult(source, cleaned, warnings);
    }

    /// <summary>
    /// Cleans SWIFT/BIC-like text by keeping alphanumerics, uppercasing, and flagging suspect length.
    /// </summary>
    /// <param name="raw">Raw OCR or source text.</param>
    /// <returns>Cleaning result with normalization warnings only (non-blocking).</returns>
    public TextCleaningResult CleanSwift(string? raw)
    {
        var source = raw ?? string.Empty;
        var cleaned = NonAlphaNumericRegex.Replace(source, string.Empty).ToUpperInvariant();

        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            warnings.Add("SwiftMissing");
        }
        else
        {
            if (cleaned.Length is not (8 or 11))
            {
                warnings.Add("SwiftLengthSuspect");
            }
            if (!string.Equals(cleaned, source, StringComparison.Ordinal))
            {
                warnings.Add("SwiftNormalized");
            }
        }

        return new TextCleaningResult(source, cleaned, warnings);
    }

    /// <summary>
    /// Performs a generic cleanup (trim + collapse whitespace) when no specific schema is known.
    /// </summary>
    /// <param name="raw">Raw OCR or source text.</param>
    /// <returns>Cleaning result with normalization warnings only (non-blocking).</returns>
    public TextCleaningResult CleanGeneric(string? raw)
    {
        var source = raw ?? string.Empty;
        var builder = new StringBuilder(source.Length);
        var lastWasSpace = false;

        foreach (var ch in source)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                builder.Append(ch);
                lastWasSpace = false;
            }
        }

        var cleaned = builder.ToString().Trim();
        var warnings = new List<string>();
        if (!string.Equals(cleaned, source, StringComparison.Ordinal))
        {
            warnings.Add("GenericNormalized");
        }

        return new TextCleaningResult(source, cleaned, warnings);
    }
}
