namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents field-level annotations showing source, confidence, and conflicts for manual review.
/// </summary>
public class FieldAnnotations
{
    /// <summary>
    /// Gets or sets the case identifier these annotations are associated with.
    /// </summary>
    public string CaseId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dictionary of field annotations with field names as keys.
    /// </summary>
    public Dictionary<string, FieldAnnotation> FieldAnnotationsDict { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldAnnotations"/> class.
    /// </summary>
    public FieldAnnotations()
    {
    }
}