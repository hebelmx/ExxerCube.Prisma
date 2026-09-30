namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Tunable thresholds and aliases for name matching.
/// </summary>
public sealed class NameMatchingOptions
{
    /// <summary>Score threshold to auto-accept a name match.</summary>
    public double AcceptThreshold { get; set; } = 0.95;

    /// <summary>Score threshold below which a conflict is flagged; between conflict and accept is review.</summary>
    public double ConflictThreshold { get; set; } = 0.80;

    /// <summary>Common aliases/variants for Spanish names/surnames (case-insensitive).</summary>
    public string[] Aliases { get; set; } = new[]
    {
        "PEREZ|PERES",
        "GONZALEZ|GONZALES",
        "CRISTIAN|CHRISTIAN",
        "CRISTINA|CHRISTINA"
    };
}
