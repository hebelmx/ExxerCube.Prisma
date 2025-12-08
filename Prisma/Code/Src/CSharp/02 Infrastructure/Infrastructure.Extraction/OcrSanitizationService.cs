namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr;

/// <summary>
/// Helper to sanitize common OCR-extracted financial identifiers (account/SWIFT) while preserving raw text.
/// </summary>
public sealed class OcrSanitizationService
{
    private readonly ITextSanitizer _sanitizer;

    /// <summary>
    /// Initializes the service with a text sanitizer for normalization.
    /// </summary>
    /// <param name="sanitizer">Injected text sanitizer.</param>
    public OcrSanitizationService(ITextSanitizer sanitizer)
    {
        _sanitizer = sanitizer;
    }

    /// <summary>
    /// Attempts to sanitize account and SWIFT-like lines from OCR text; best effort and non-blocking.
    /// </summary>
    public SanitizedOcrValues SanitizeAccountAndSwift(string text)
    {
        var lines = (text ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .ToArray();

        var accountLine = lines.FirstOrDefault(l => l.Contains("CUENTA", StringComparison.OrdinalIgnoreCase));
        var swiftLine = lines.FirstOrDefault(l => l.Contains("SWIFT", StringComparison.OrdinalIgnoreCase));

        var account = _sanitizer.CleanAccount(accountLine);
        var swift = _sanitizer.CleanSwift(swiftLine);

        // If SWIFT was normalized due to noise and account appears clean, still surface a soft normalization warning for account.
        if (account.Warnings.Count == 0 &&
            !string.IsNullOrWhiteSpace(account.Raw) &&
            swift.Warnings.Contains("SwiftNormalized", StringComparer.OrdinalIgnoreCase))
        {
            var mergedWarnings = account.Warnings.ToList();
            mergedWarnings.Add("AccountNormalized");
            account = new TextCleaningResult(account.Raw, account.Cleaned, mergedWarnings);
        }

        return new SanitizedOcrValues(text ?? string.Empty, account, swift);
    }
}
