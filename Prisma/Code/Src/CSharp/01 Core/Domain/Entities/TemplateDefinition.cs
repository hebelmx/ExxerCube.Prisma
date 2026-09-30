namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents a bank export template definition with versioning and field mappings.
/// </summary>
/// <remarks>
/// <para>
/// Stores the complete structure of an export template (Excel, XML, PDF) including
/// field mappings from <see cref="ValueObjects.UnifiedMetadataRecord"/> to the target format.
/// </para>
/// <para>
/// <strong>Template Types:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>Excel: SIRO registration layout templates</description></item>
///   <item><description>XML: SIRO-compliant XML export schemas</description></item>
///   <item><description>PDF: Digitally signed PDF document templates</description></item>
/// </list>
/// <para>
/// Templates are versioned using semantic versioning to support schema evolution
/// and backward compatibility.
/// </para>
/// </remarks>
public class TemplateDefinition
{
    /// <summary>
    /// Gets or sets the unique identifier for the template.
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the template type (Excel, XML, PDF).
    /// </summary>
    public string TemplateType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the semantic version of the template (e.g., "1.0.0", "2.5.3").
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the friendly name for the template.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the template description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date when this template becomes effective.
    /// </summary>
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets the date when this template expires (nullable).
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Gets or sets whether this template is active and should be used for exports.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the list of field mappings for this template.
    /// </summary>
    public List<ValueObjects.FieldMapping> FieldMappings { get; set; } = new();

    /// <summary>
    /// Gets or sets the XML namespace (for XML templates only).
    /// </summary>
    public string? XmlNamespace { get; set; }

    /// <summary>
    /// Gets or sets the root element name (for XML templates only).
    /// </summary>
    public string? RootElement { get; set; }

    /// <summary>
    /// Gets or sets additional template metadata (format-specific configuration).
    /// </summary>
    public Dictionary<string, string?> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the date when the template was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who created the template.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date when the template was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who last modified the template.
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateDefinition"/> class.
    /// </summary>
    public TemplateDefinition()
    {
    }
}
