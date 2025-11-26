namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents the complete comparison result between XML and OCR extracted data.
/// </summary>
public class ComparisonResult
{
    /// <summary>
    /// Gets or sets the list of individual field comparisons.
    /// </summary>
    public List<FieldComparison> FieldComparisons { get; set; } = new();

    /// <summary>
    /// Gets or sets the overall similarity score (0.0 to 1.0).
    /// Calculated as the average of all field similarities.
    /// </summary>
    public float OverallSimilarity { get; set; }

    /// <summary>
    /// Gets or sets the count of exact matches.
    /// </summary>
    public int MatchCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of fields compared.
    /// </summary>
    public int TotalFields { get; set; }

    /// <summary>
    /// Gets or sets the percentage of fields that match exactly.
    /// </summary>
    public float MatchPercentage => TotalFields > 0 ? (MatchCount / (float)TotalFields) * 100f : 0f;
}
