namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Field-specific matching rule configuration.
/// </summary>
public class FieldMatchingRule
{
    /// <summary>
    /// Gets or sets the conflict threshold for this specific field (overrides global threshold).
    /// </summary>
    public float? ConflictThreshold { get; set; }

    /// <summary>
    /// Gets or sets the minimum confidence required for this specific field (overrides global minimum).
    /// </summary>
    public float? MinimumConfidence { get; set; }

    /// <summary>
    /// Gets or sets the source priority for this specific field (overrides global priority).
    /// </summary>
    public List<string>? SourcePriority { get; set; }
}