namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Extraction mode for adaptive DOCX extraction.
/// </summary>
public enum ExtractionMode
{
    /// <summary>
    /// Use the strategy with the highest confidence score.
    /// </summary>
    /// <remarks>
    /// Queries all strategies, selects the one with highest confidence,
    /// and returns its extraction results.
    /// </remarks>
    BestStrategy = 0,

    /// <summary>
    /// Merge results from all strategies that can handle the document.
    /// </summary>
    /// <remarks>
    /// Runs all strategies with confidence > 0, merges results using
    /// <see cref="IFieldMergeStrategy"/>.
    /// </remarks>
    MergeAll = 1,

    /// <summary>
    /// Fill gaps in existing fields without overwriting.
    /// </summary>
    /// <remarks>
    /// Uses strategies to extract missing fields only, preserving existing data.
    /// Requires <c>existingFields</c> parameter to be provided.
    /// </remarks>
    Complement = 2
}