namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr;

/// <summary>
/// Default OCR text cleaner that normalizes noisy captures while keeping the raw text and warnings.
/// </summary>
public sealed class TextSanitizer : ITextSanitizer
{
    private static readonly Regex NonDigitRegex = new(@"[^\d]", RegexOptions.Compiled);
    private static readonly Regex NonAlphaNumericRegex = new(@"[^A-Za-z0-9]", RegexOptions.Compiled);

    // Common OCR label prefixes to strip before cleaning
    private static readonly Regex SwiftLabelPrefixRegex = new(@"^\s*(SWIFT|BIC|CODIGO|CODE|SWIFT\s*CODE|BIC\s*CODE)\s*[:.\-]?\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AccountLabelPrefixRegex = new(@"^\s*(CUENTA|ACCOUNT|CTA|NO\s*DE\s*CUENTA|NUM|NUMERO|NUMBER)\s*[:.\-]?\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
        // Strip common OCR label prefixes first (CUENTA:, ACCOUNT:, etc.)
        var withoutLabel = AccountLabelPrefixRegex.Replace(source, string.Empty);
        var cleaned = NonDigitRegex.Replace(withoutLabel, string.Empty);

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
            // Only flag as normalized if the account digits themselves changed (not just label stripping)
            if (!string.Equals(cleaned, withoutLabel, StringComparison.Ordinal))
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
        // Strip common OCR label prefixes first (SWIFT:, BIC:, etc.)
        var withoutLabel = SwiftLabelPrefixRegex.Replace(source, string.Empty);
        var cleaned = NonAlphaNumericRegex.Replace(withoutLabel, string.Empty).ToUpperInvariant();

        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            warnings.Add("SwiftMissing");
        }
        else
        {
            var normalized = !string.Equals(cleaned, withoutLabel, StringComparison.Ordinal);
            var hadWhitespace = withoutLabel.Any(char.IsWhiteSpace);

            // If we are only missing the branch code (9 chars) and we already normalized noise, pad to 11.
            var padded = false;
            var shouldPad = normalized && cleaned.Length == 9 && !hadWhitespace;
            if (shouldPad)
            {
                cleaned = cleaned.PadRight(11, 'X');
                padded = true;
            }

            var lengthIsStandard = cleaned.Length is 8 or 11 || (!shouldPad && cleaned.Length == 9);
            if (!lengthIsStandard)
            {
                warnings.Add("SwiftLengthSuspect");
            }

            // Only flag as normalized if the SWIFT code itself changed (noise removal or padding)
            if (padded || normalized)
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
