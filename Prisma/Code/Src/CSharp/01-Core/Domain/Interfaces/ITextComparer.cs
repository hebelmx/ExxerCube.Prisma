namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Interface for comparing text strings and calculating similarity/distance metrics.
/// Used for OCR quality assessment, validation, and improvement measurement.
/// Provides both precise edit distance (Levenshtein) and fuzzy matching (FuzzySharp).
/// </summary>
public interface ITextComparer
{
    /// <summary>
    /// Calculates the Levenshtein edit distance between two strings.
    /// The Levenshtein distance is the minimum number of single-character edits
    /// (insertions, deletions, or substitutions) required to change one string into another.
    /// </summary>
    /// <param name="source">First string (e.g., ground truth or baseline OCR result).</param>
    /// <param name="target">Second string (e.g., OCR result or enhanced OCR result).</param>
    /// <returns>Minimum edit distance. Lower distance = higher similarity.</returns>
    int CalculateEditDistance(string source, string target);

    /// <summary>
    /// Calculates the fuzzy similarity ratio between two strings using FuzzySharp.
    /// More forgiving than Levenshtein distance - handles character reorderings and partial matches better.
    /// Returns a value between 0 (completely different) and 100 (identical).
    /// </summary>
    /// <param name="source">First string.</param>
    /// <param name="target">Second string.</param>
    /// <returns>Fuzzy similarity score between 0 and 100.</returns>
    int CalculateFuzzyRatio(string source, string target);

    /// <summary>
    /// Calculates the similarity percentage between two strings based on edit distance.
    /// Returns a value between 0.0 (completely different) and 1.0 (identical).
    /// </summary>
    /// <param name="source">First string.</param>
    /// <param name="target">Second string.</param>
    /// <returns>Similarity score between 0.0 and 1.0.</returns>
    double CalculateSimilarity(string source, string target);

    /// <summary>
    /// Calculates a quality score for text based on multiple indicators.
    /// Analyzes alphanumeric ratio, whitespace balance, special characters, and word validity.
    /// Higher scores indicate better text quality (typical OCR output).
    /// </summary>
    /// <param name="text">Text to analyze.</param>
    /// <returns>Quality score between 0 and 100.</returns>
    double CalculateQualityScore(string text);
}
