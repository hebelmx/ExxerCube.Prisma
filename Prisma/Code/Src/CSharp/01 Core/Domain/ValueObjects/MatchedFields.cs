namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents field matching results across XML, DOCX, and PDF sources.
/// </summary>
public class MatchedFields
{
    /// <summary>
    /// Gets or sets the dictionary of matched fields with their field names as keys.
    /// </summary>
    public Dictionary<string, FieldMatchResult> FieldMatches { get; set; } = new();

    /// <summary>
    /// Gets or sets the overall agreement level across all fields (0.0-1.0).
    /// </summary>
    public float OverallAgreement { get; set; }

    /// <summary>
    /// Gets or sets the list of field names with conflicting values across sources.
    /// </summary>
    public List<string> ConflictingFields { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of field names that are missing from all sources.
    /// </summary>
    public List<string> MissingFields { get; set; } = new();

    /// <summary>
    /// Gets or sets merged additional fields (beyond core fields) and their values.
    /// </summary>
    public Dictionary<string, string?> AdditionalMerged { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of additional field names with conflicts across sources.
    /// </summary>
    public List<string> AdditionalConflicts { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MatchedFields"/> class.
    /// </summary>
    public MatchedFields()
    {
    }
}

