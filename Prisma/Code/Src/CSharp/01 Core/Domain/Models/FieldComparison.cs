namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents the comparison result of a single field between XML and OCR sources.
/// </summary>
public class FieldComparison
{
    /// <summary>
    /// Gets or sets the name of the field being compared.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value from the XML source.
    /// </summary>
    public string XmlValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value from the OCR source.
    /// </summary>
    public string OcrValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status of the comparison.
    /// Possible values: "Match", "Partial", "Different", "Missing"
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Gets or sets the similarity score between values (0.0 to 1.0).
    /// 1.0 = exact match, 0.0 = completely different.
    /// </summary>
    public float Similarity { get; set; }

    /// <summary>
    /// Gets or sets the OCR confidence for this field (if available).
    /// </summary>
    public float? OcrConfidence { get; set; }
}
