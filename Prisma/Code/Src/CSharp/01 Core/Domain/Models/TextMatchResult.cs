namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents the result of a fuzzy text matching operation.
/// Contains the matched substring, similarity score, and position information.
/// Used by ITextComparer.FindBestMatch for dictionary-based classification.
/// </summary>
public sealed class TextMatchResult
{
    /// <summary>
    /// The actual text substring that was matched in the document.
    /// This may differ from the search phrase due to fuzzy matching.
    /// Example: Search="aseguramiento de fondos", Match="aseguramiento de los fondos"
    /// </summary>
    public required string MatchedText { get; init; }

    /// <summary>
    /// Similarity score between the search phrase and matched text.
    /// Value between 0.0 (completely different) and 1.0 (identical).
    /// Typical threshold is 0.85 (85% similar).
    /// </summary>
    public required double Similarity { get; init; }

    /// <summary>
    /// Starting character index of the matched text in the original document.
    /// Zero-based index.
    /// </summary>
    public required int StartIndex { get; init; }

    /// <summary>
    /// Length of the matched text in characters.
    /// </summary>
    public required int Length { get; init; }

    /// <summary>
    /// Ending character index of the matched text (exclusive).
    /// Calculated as StartIndex + Length.
    /// </summary>
    public int EndIndex => StartIndex + Length;

    /// <summary>
    /// Similarity percentage (0-100).
    /// Convenience property for displaying similarity as percentage.
    /// </summary>
    public double SimilarityPercentage => Similarity * 100.0;

    /// <summary>
    /// Returns a string representation of the match result.
    /// </summary>
    /// <returns>A formatted string containing matched text, similarity percentage, and position.</returns>
    public override string ToString() =>
        $"Match: '{MatchedText}' (Similarity: {SimilarityPercentage:F1}%, Position: {StartIndex}-{EndIndex})";
}
