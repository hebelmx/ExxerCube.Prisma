namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr;

/// <summary>
/// Captures raw OCR text plus sanitized account and SWIFT values.
/// </summary>
public sealed class SanitizedOcrValues
{
    /// <summary>
    /// Initializes a new instance capturing raw OCR plus cleaned account/SWIFT values.
    /// </summary>
    /// <param name="rawText">Full OCR text as captured.</param>
    /// <param name="account">Sanitized account result.</param>
    /// <param name="swift">Sanitized SWIFT result.</param>
    public SanitizedOcrValues(string rawText, TextCleaningResult account, TextCleaningResult swift)
    {
        RawText = rawText;
        Account = account;
        Swift = swift;
    }

    /// <summary>Full raw OCR text.</summary>
    public string RawText { get; }

    /// <summary>Sanitized account number result (raw + cleaned + warnings).</summary>
    public TextCleaningResult Account { get; }

    /// <summary>Sanitized SWIFT/BIC result (raw + cleaned + warnings).</summary>
    public TextCleaningResult Swift { get; }
}
