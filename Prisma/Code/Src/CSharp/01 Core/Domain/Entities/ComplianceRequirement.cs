namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents a compliance requirement from a regulatory directive.
/// </summary>
public class ComplianceRequirement
{
    /// <summary>
    /// Gets or sets the requirement identifier.
    /// </summary>
    public string RequerimientoId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requirement description.
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requirement type.
    /// </summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the requirement is mandatory.
    /// </summary>
    public bool EsObligatorio { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceRequirement"/> class.
    /// </summary>
    public ComplianceRequirement()
    {
    }
}

