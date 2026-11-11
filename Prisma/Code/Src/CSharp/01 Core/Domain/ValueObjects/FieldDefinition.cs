namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Defines a field to be extracted from a document source.
/// </summary>
public class FieldDefinition
{
    /// <summary>
    /// Gets or sets the field name (e.g., "Expediente", "Causa", "AccionSolicitada").
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field type (e.g., "string", "date", "amount").
    /// </summary>
    public string FieldType { get; set; } = "string";

    /// <summary>
    /// Gets or sets a value indicating whether this field is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldDefinition"/> class.
    /// </summary>
    public FieldDefinition()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldDefinition"/> class with specified values.
    /// </summary>
    /// <param name="fieldName">The field name.</param>
    /// <param name="fieldType">The field type.</param>
    /// <param name="isRequired">Whether the field is required.</param>
    public FieldDefinition(string fieldName, string fieldType = "string", bool isRequired = false)
    {
        FieldName = fieldName;
        FieldType = fieldType;
        IsRequired = isRequired;
    }
}

