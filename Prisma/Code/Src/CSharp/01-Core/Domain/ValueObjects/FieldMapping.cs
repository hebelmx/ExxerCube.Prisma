namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents a field mapping from source data to template format.
/// </summary>
/// <remarks>
/// <para>
/// Defines how to map a field from <see cref="UnifiedMetadataRecord"/> to a specific
/// position/element in the export template (Excel column, XML element, PDF field).
/// </para>
/// <para>
/// <strong>Source Field Path Examples:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>"Expediente.NumeroExpediente" - Simple nested property</description></item>
///   <item><description>"Personas[0].Nombre" - Collection with index</description></item>
///   <item><description>"ComplianceActions[0].AccountNumber" - Nested collection property</description></item>
/// </list>
/// <para>
/// <strong>Target Field Examples:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>Excel: Column index (1, 2, 3) or name ("A", "B", "NumeroExpediente")</description></item>
///   <item><description>XML: Element name or XPath ("/SiroResponse/NumeroExpediente")</description></item>
///   <item><description>PDF: Field name in PDF form</description></item>
/// </list>
/// </remarks>
public class FieldMapping
{
    /// <summary>
    /// Gets or sets the unique identifier for this field mapping.
    /// </summary>
    public string MappingId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source field path (e.g., "Expediente.NumeroExpediente").
    /// </summary>
    /// <remarks>
    /// Uses dot notation for nested properties and bracket notation for collections.
    /// </remarks>
    public string SourceFieldPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target field identifier in the export format.
    /// </summary>
    /// <remarks>
    /// Format depends on template type:
    /// <list type="bullet">
    ///   <item><description>Excel: Column name or index</description></item>
    ///   <item><description>XML: Element name or XPath</description></item>
    ///   <item><description>PDF: Form field name</description></item>
    /// </list>
    /// </remarks>
    public string TargetField { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target field display name/header (e.g., "Número de Expediente").
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets whether this field is required in the export.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the expected data type for the field.
    /// </summary>
    /// <remarks>
    /// Examples: "string", "int", "decimal", "DateTime", "bool"
    /// </remarks>
    public string DataType { get; set; } = "string";

    /// <summary>
    /// Gets or sets the format string for the field (e.g., "yyyy-MM-dd" for dates).
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets the default value to use when source field is null or missing.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum length for string fields.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Gets or sets the display order/position in the template.
    /// </summary>
    /// <remarks>
    /// Used for Excel column order, XML element sequence, etc.
    /// </remarks>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Gets or sets whether this field supports null values.
    /// </summary>
    public bool IsNullable { get; set; } = true;

    /// <summary>
    /// Gets or sets a transformation expression to apply to the source value.
    /// </summary>
    /// <remarks>
    /// Examples: "ToUpper()", "Trim()", "Substring(0, 10)"
    /// </remarks>
    public string? TransformExpression { get; set; }

    /// <summary>
    /// Gets or sets validation rules for the field value.
    /// </summary>
    /// <remarks>
    /// Examples: "Regex:^[A-Z0-9-]+$", "Range:1,100", "EmailAddress"
    /// </remarks>
    public List<string> ValidationRules { get; set; } = new();

    /// <summary>
    /// Gets or sets additional metadata for this field mapping (format-specific).
    /// </summary>
    public Dictionary<string, string?> Metadata { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldMapping"/> class.
    /// </summary>
    public FieldMapping()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldMapping"/> class with core properties.
    /// </summary>
    /// <param name="sourceFieldPath">The source field path in dot notation.</param>
    /// <param name="targetField">The target field identifier in the export format.</param>
    /// <param name="isRequired">Whether the field is required.</param>
    /// <param name="dataType">The expected data type.</param>
    public FieldMapping(string sourceFieldPath, string targetField, bool isRequired, string dataType = "string")
    {
        SourceFieldPath = sourceFieldPath;
        TargetField = targetField;
        IsRequired = isRequired;
        DataType = dataType;
    }
}
