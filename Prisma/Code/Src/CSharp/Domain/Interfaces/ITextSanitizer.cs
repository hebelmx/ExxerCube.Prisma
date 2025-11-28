namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Provides OCR/text cleaning utilities so we can normalize noisy captures while keeping the raw text for audit.
/// </summary>
public interface ITextSanitizer
{
    /// <summary>
    /// Cleans account-number–like text (removes artifacts, keeps digits only).
    /// </summary>
    TextCleaningResult CleanAccount(string? raw);

    /// <summary>
    /// Cleans SWIFT/BIC–like text (alphanumeric, uppercased, strips spacing/punctuation).
    /// </summary>
    TextCleaningResult CleanSwift(string? raw);

    /// <summary>
    /// Generic cleanup for OCR text when no specific schema is known (trims, collapses spaces).
    /// </summary>
    TextCleaningResult CleanGeneric(string? raw);
}
