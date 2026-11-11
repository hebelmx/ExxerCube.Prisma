namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents annotation information for a single field.
/// </summary>
public class FieldAnnotation
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current field value.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets or sets the confidence score for this field (0-100).
    /// </summary>
    public int Confidence { get; set; }

    /// <summary>
    /// Gets or sets the source type from which this field was extracted (XML, DOCX, PDF, OCR).
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this field has conflicting values across sources.
    /// </summary>
    public bool HasConflict { get; set; }

    /// <summary>
    /// Gets or sets the agreement level across sources (0.0-1.0, where 1.0 means all sources agree).
    /// </summary>
    public float AgreementLevel { get; set; }

    /// <summary>
    /// Gets or sets the origin trace showing where this field value came from.
    /// </summary>
    public string OriginTrace { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of all values found across sources (for conflict detection).
    /// </summary>
    public List<object> AllSourceValues { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldAnnotation"/> class.
    /// </summary>
    public FieldAnnotation()
    {
    }
}