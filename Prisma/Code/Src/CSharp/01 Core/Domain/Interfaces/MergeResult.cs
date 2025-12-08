namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Result of a merge operation containing merged data and diagnostics.
/// </summary>
public sealed class MergeResult
{
    /// <summary>
    /// Gets or sets the merged extracted fields.
    /// </summary>
    /// <remarks>
    /// Never null. Contains empty ExtractedFields if merge produced no data.
    /// </remarks>
    public ExtractedFields MergedFields { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of fields that had conflicting values during merge.
    /// </summary>
    /// <remarks>
    /// Each entry describes a field where multiple sources provided different values.
    /// Empty if no conflicts occurred.
    /// </remarks>
    public List<FieldConflict> Conflicts { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of fields that were successfully merged.
    /// </summary>
    /// <remarks>
    /// Useful for diagnostics and understanding merge behavior.
    /// </remarks>
    public List<string> MergedFieldNames { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of source field sets that contributed to the merge.
    /// </summary>
    public int SourceCount { get; set; }
}