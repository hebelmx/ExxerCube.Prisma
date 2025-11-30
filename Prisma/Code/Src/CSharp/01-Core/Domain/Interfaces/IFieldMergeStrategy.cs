namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Strategy for merging extracted fields from multiple sources.
/// </summary>
/// <remarks>
/// <para>
/// Implementations define how to combine <see cref="ExtractedFields"/> from multiple
/// extraction strategies, handling conflicts and filling gaps.
/// </para>
/// <para>
/// <strong>Common Merge Policies:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>First-wins: Use first non-null value encountered</description></item>
///   <item><description>Last-wins: Use last non-null value encountered</description></item>
///   <item><description>Longest: Use longest string value</description></item>
///   <item><description>Most-complete: Use value from most complete source</description></item>
///   <item><description>Fuzzy-match: Use intelligent matching for names, amounts</description></item>
/// </list>
/// </remarks>
public interface IFieldMergeStrategy
{
    /// <summary>
    /// Merges multiple extracted field sets into a single result.
    /// </summary>
    /// <param name="fieldSets">Collection of extracted fields to merge, in priority order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Merged fields result containing combined data and conflict information.
    /// Never returns null; returns empty result if no fields provided.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The order of <paramref name="fieldSets"/> may influence merge decisions
    /// depending on implementation strategy.
    /// </para>
    /// <para>
    /// <strong>Contract Guarantees:</strong>
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Never returns null (returns empty MergeResult instead)</description></item>
    ///   <item><description>Handles null entries in fieldSets gracefully</description></item>
    ///   <item><description>Preserves all non-conflicting data</description></item>
    ///   <item><description>Reports conflicts in MergeResult.Conflicts</description></item>
    /// </list>
    /// </remarks>
    Task<MergeResult> MergeAsync(
        IReadOnlyList<ExtractedFields?> fieldSets,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges two field sets with explicit priority.
    /// </summary>
    /// <param name="primary">Primary field set (higher priority).</param>
    /// <param name="secondary">Secondary field set (used to fill gaps).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Merged fields result. Never returns null.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is a convenience method for the common case of merging exactly two sources.
    /// Equivalent to calling <c>MergeAsync(new[] { primary, secondary })</c>.
    /// </para>
    /// <para>
    /// <strong>Merge Behavior:</strong>
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Primary values always take precedence when present</description></item>
    ///   <item><description>Secondary values fill gaps in primary</description></item>
    ///   <item><description>Collections (Fechas, Montos) are combined and deduplicated</description></item>
    /// </list>
    /// </remarks>
    Task<MergeResult> MergeAsync(
        ExtractedFields? primary,
        ExtractedFields? secondary,
        CancellationToken cancellationToken = default);
}

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
