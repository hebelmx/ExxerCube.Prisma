using System;

namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents the outcome of cleaning OCR text, retaining the raw and normalized forms plus any warnings raised.
/// </summary>
public sealed class TextCleaningResult
{
    /// <summary>
    /// Initializes a new instance capturing raw and cleaned text plus any warnings.
    /// </summary>
    /// <param name="raw">Original captured text (as-is).</param>
    /// <param name="cleaned">Normalized text after applying cleaning rules.</param>
    /// <param name="warnings">Non-blocking warnings raised during cleaning.</param>
    public TextCleaningResult(string raw, string cleaned, IReadOnlyCollection<string> warnings)
    {
        Raw = raw ?? string.Empty;
        Cleaned = cleaned ?? string.Empty;
        Warnings = warnings ?? Array.Empty<string>();
    }

    /// <summary>Original captured text (as-is from OCR/source).</summary>
    public string Raw { get; }

    /// <summary>Normalized text after applying cleaning rules.</summary>
    public string Cleaned { get; }

    /// <summary>Non-blocking warnings describing adjustments or potential issues.</summary>
    public IReadOnlyCollection<string> Warnings { get; }
}
