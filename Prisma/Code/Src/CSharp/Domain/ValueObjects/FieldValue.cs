namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents a field value extracted from a document source.
/// </summary>
public class FieldValue
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the extracted value as a string.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the confidence score for this extraction (0.0-1.0).
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Gets or sets the source type from which this value was extracted.
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldValue"/> class.
    /// </summary>
    public FieldValue()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldValue"/> class with specified values.
    /// </summary>
    /// <param name="fieldName">The field name.</param>
    /// <param name="value">The extracted value.</param>
    /// <param name="confidence">The confidence score.</param>
    /// <param name="sourceType">The source type.</param>
    public FieldValue(string fieldName, string? value, float confidence, string sourceType)
    {
        FieldName = fieldName;
        Value = value;
        Confidence = confidence;
        SourceType = sourceType;
    }
}

