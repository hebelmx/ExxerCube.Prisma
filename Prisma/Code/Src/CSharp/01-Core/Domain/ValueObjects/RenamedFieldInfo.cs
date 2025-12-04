namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents a field that may have been renamed (fuzzy match).
/// </summary>
public sealed class RenamedFieldInfo
{
    /// <summary>
    /// Gets the old field path (from template).
    /// </summary>
    public string OldFieldPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the suggested new field path (from source).
    /// </summary>
    public string SuggestedNewFieldPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the similarity score (0.0 to 1.0).
    /// Higher scores indicate stronger match confidence.
    /// </summary>
    public double SimilarityScore { get; init; }

    /// <summary>
    /// Gets the target field name in the template.
    /// </summary>
    public string TargetField { get; init; } = string.Empty;
}