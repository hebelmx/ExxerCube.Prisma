namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents a person (physical or legal entity) involved in a regulatory case.
/// </summary>
public class Persona
{
    /// <summary>
    /// Gets or sets the party identifier.
    /// </summary>
    public int ParteId { get; set; }

    /// <summary>
    /// Gets or sets the character/role (e.g., "Patrón Determinado", "Contribuyente Auditado").
    /// </summary>
    public string Caracter { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the person type ("Fisica" or "Moral").
    /// </summary>
    public string PersonaTipo { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last name (paternal).
    /// </summary>
    public string? Paterno { get; set; }

    /// <summary>
    /// Gets or sets the last name (maternal).
    /// </summary>
    public string? Materno { get; set; }

    /// <summary>
    /// Gets or sets the first name.
    /// </summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RFC (Tax identification number).
    /// </summary>
    public string? Rfc { get; set; }

    /// <summary>
    /// Gets or sets the relationship to the case.
    /// </summary>
    public string? Relacion { get; set; }

    /// <summary>
    /// Gets or sets the address.
    /// </summary>
    public string? Domicilio { get; set; }

    /// <summary>
    /// Gets or sets additional information (CURP, birth date, etc.).
    /// </summary>
    public string? Complementarios { get; set; }

    /// <summary>
    /// Gets or sets the RFC variants for identity resolution.
    /// </summary>
    public List<string> RfcVariants { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Persona"/> class.
    /// </summary>
    public Persona()
    {
    }
}

