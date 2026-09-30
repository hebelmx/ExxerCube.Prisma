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

    /// <summary>
    /// Finds the best matching phrase in a document text using fuzzy matching.
    /// Uses sliding window approach to find the substring that best matches the search phrase.
    /// Critical for dictionary-based classification where exact phrase matches are rare.
    /// </summary>
    /// <param name="phrase">The phrase to search for (e.g., "aseguramiento de fondos").</param>
    /// <param name="text">The document text to search within.</param>
    /// <param name="threshold">Minimum similarity threshold (0.0-1.0). Default is 0.85 (85% match).</param>
    /// <returns>TextMatchResult with matched text and similarity score, or null if no match above threshold.</returns>
    TextMatchResult? FindBestMatch(string phrase, string text, double threshold = 0.85);
}
