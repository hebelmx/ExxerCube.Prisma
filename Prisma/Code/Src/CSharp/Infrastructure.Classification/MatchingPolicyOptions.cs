using System.Collections.Generic;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Configuration options for field matching policies.
/// </summary>
public class MatchingPolicyOptions
{
    /// <summary>
    /// Gets or sets the conflict threshold for determining if field values are in conflict (0.0-1.0).
    /// Default: 0.5 (values must agree at least 50%).
    /// </summary>
    public float ConflictThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Gets or sets the minimum confidence score required for a field match to be considered valid (0.0-1.0).
    /// Default: 0.3 (minimum 30% confidence).
    /// </summary>
    public float MinimumConfidence { get; set; } = 0.3f;

    /// <summary>
    /// Gets or sets a value indicating whether to prefer values from specific source types.
    /// Order matters - first source type has highest priority.
    /// </summary>
    public List<string> SourcePriority { get; set; } = new() { "XML", "DOCX", "PDF" };

    /// <summary>
    /// Gets or sets field-specific matching rules.
    /// </summary>
    public Dictionary<string, FieldMatchingRule> FieldRules { get; set; } = new();
}

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

