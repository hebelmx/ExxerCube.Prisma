namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Orchestrator for adaptive DOCX field extraction using multiple strategies.
/// </summary>
/// <remarks>
/// <para>
/// Coordinates multiple <see cref="IAdaptiveDocxStrategy"/> implementations to extract
/// fields from DOCX documents, selecting the best strategy or combining results.
/// </para>
/// <para>
/// <strong>Extraction Modes:</strong>
/// </para>
/// <list type="bullet">
///   <item><description><see cref="ExtractionMode.BestStrategy"/>: Use highest confidence strategy</description></item>
///   <item><description><see cref="ExtractionMode.MergeAll"/>: Combine results from all strategies</description></item>
///   <item><description><see cref="ExtractionMode.Complement"/>: Fill gaps in existing data</description></item>
/// </list>
/// </remarks>
public interface IAdaptiveDocxExtractor
{
    /// <summary>
    /// Extracts fields from DOCX text using adaptive strategy selection.
    /// </summary>
    /// <param name="docxText">The DOCX document text to extract from.</param>
    /// <param name="mode">The extraction mode (default: BestStrategy).</param>
    /// <param name="existingFields">Existing fields to complement (used with Complement mode).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Extracted fields if successful; otherwise, null.
    /// Returns null when no strategies can handle the document or extraction fails.
    /// </returns>
    Task<ExtractedFields?> ExtractAsync(
        string docxText,
        ExtractionMode mode = ExtractionMode.BestStrategy,
        ExtractedFields? existingFields = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available strategies with their confidence scores for the given document.
    /// </summary>
    /// <param name="docxText">The DOCX document text to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// List of tuples containing strategy name and confidence score, sorted by confidence descending.
    /// Empty list if no strategies are available.
    /// </returns>
    Task<IReadOnlyList<StrategyConfidence>> GetStrategyConfidencesAsync(
        string docxText,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Strategy confidence information.
/// </summary>
/// <param name="StrategyName">Name of the strategy.</param>
/// <param name="Confidence">Confidence score (0-100).</param>
public record StrategyConfidence(string StrategyName, int Confidence);

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
