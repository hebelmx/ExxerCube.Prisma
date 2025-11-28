namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Result of merging additional fields from multiple sources.
/// </summary>
public class AdditionalFieldMergeResult
{
    /// <summary>
    /// Gets the merged fields.
    /// </summary>
    public Dictionary<string, string?> Merged { get; } = new();

    /// <summary>
    /// Gets the list of field names that resulted in conflicts.
    /// </summary>
    public List<string> Conflicts { get; } = new();
}
