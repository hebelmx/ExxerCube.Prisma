using ExxerCube.Prisma.Domain.Enums;

namespace ExxerCube.Prisma.Domain.ValueObjects;

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
    /// Gets or sets the origin of this value (XML, PDF/OCR, DOCX, Derived, Manual).
    /// </summary>
    public FieldOrigin Origin { get; set; } = FieldOrigin.Unknown;

    /// <summary>
    /// Gets or sets the raw value before sanitization (useful for OCR audit).
    /// </summary>
    public string? RawValue { get; set; }

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
    /// <param name="origin">The origin of the value.</param>
    /// <param name="rawValue">The raw (unsanitized) value, if available.</param>
    public FieldValue(string fieldName, string? value, float confidence, string sourceType, FieldOrigin origin = FieldOrigin.Unknown, string? rawValue = null)
    {
        FieldName = fieldName;
        Value = value;
        Confidence = confidence;
        SourceType = sourceType;
        Origin = origin;
        RawValue = rawValue;
    }
}
