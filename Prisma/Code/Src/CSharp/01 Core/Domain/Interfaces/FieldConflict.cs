namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Describes a conflict that occurred during field merging.
/// </summary>
public sealed class FieldConflict
{
    /// <summary>
    /// Gets or sets the name of the field that had conflicting values.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of conflicting values from different sources.
    /// </summary>
    public List<string> ConflictingValues { get; set; } = new();

    /// <summary>
    /// Gets or sets the value that was chosen as the final merged value.
    /// </summary>
    public string? ResolvedValue { get; set; }

    /// <summary>
    /// Gets or sets the resolution strategy used (e.g., "first-wins", "fuzzy-match").
    /// </summary>
    public string ResolutionStrategy { get; set; } = string.Empty;
}