using ExxerCube.Prisma.Domain.Enums;

namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents the result of matching a single field across multiple sources.
/// </summary>
public class FieldMatchResult
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the matched value (best value selected from all sources).
    /// </summary>
    public string? MatchedValue { get; set; }

    /// <summary>
    /// Gets or sets the confidence score for the matched value (0.0-1.0).
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Gets or sets the source type from which the matched value was selected.
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the origin for the matched value (XML, PDF/OCR, DOCX, Derived, Manual).
    /// </summary>
    public FieldOrigin Origin { get; set; } = FieldOrigin.Unknown;

    /// <summary>
    /// Gets or sets the raw value before sanitization (useful for audit when OCR was cleaned).
    /// </summary>
    public string? RawValue { get; set; }

    /// <summary>
    /// Gets or sets the list of all values found across sources (for conflict detection).
    /// </summary>
    public List<FieldValue> AllValues { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether there are conflicting values across sources.
    /// </summary>
    public bool HasConflict { get; set; }

    /// <summary>
    /// Gets or sets the agreement level across sources (0.0-1.0, where 1.0 means all sources agree).
    /// </summary>
    public float AgreementLevel { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldMatchResult"/> class.
    /// </summary>
    public FieldMatchResult()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldMatchResult"/> class with specified values.
    /// </summary>
    /// <param name="fieldName">The field name.</param>
    /// <param name="matchedValue">The matched value.</param>
    /// <param name="confidence">The confidence score.</param>
    /// <param name="sourceType">The source type.</param>
    /// <param name="origin">The origin.</param>
    /// <param name="rawValue">The raw value (unsanitized), if available.</param>
    public FieldMatchResult(string fieldName, string? matchedValue, float confidence, string sourceType, FieldOrigin origin = FieldOrigin.Unknown, string? rawValue = null)
    {
        FieldName = fieldName;
        MatchedValue = matchedValue;
        Confidence = confidence;
        SourceType = sourceType;
        Origin = origin;
        RawValue = rawValue;
    }
}
