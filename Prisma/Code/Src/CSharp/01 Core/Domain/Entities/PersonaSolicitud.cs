using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents a person about whom information is requested in a specific solicitud (SolicitudEspecifica).
/// Similar to SolicitudParte but in the context of SolicitudEspecifica.
/// </summary>
/// <remarks>
/// This class maps to the PersonasSolicitud XML element found within SolicitudEspecifica.
/// Based on real PRP1 fixtures from CNBV.
/// </remarks>
public class PersonaSolicitud
{
    /// <summary>
    /// Gets or sets the person identifier.
    /// </summary>
    public int PersonaId { get; set; }

    /// <summary>
    /// Gets or sets the character/role (e.g., "Patrón Determinado", "Tercero vinculado").
    /// </summary>
    public string Caracter { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the person type ("Fisica" or "Moral").
    /// </summary>
    /// <remarks>
    /// XML element name is "Persona" (not "PersonaTipo").
    /// Values: "Fisica" (individual) or "Moral" (legal entity).
    /// </remarks>
    public string Persona { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the paternal last name (optional for Moral persons).
    /// </summary>
    public string? Paterno { get; set; }

    /// <summary>
    /// Gets or sets the maternal last name (optional for Moral persons).
    /// </summary>
    public string? Materno { get; set; }

    /// <summary>
    /// Gets or sets the name (first name for Fisica, company name for Moral).
    /// </summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RFC (Tax identification number).
    /// </summary>
    public string? Rfc { get; set; }

    /// <summary>
    /// Gets the RFC variants captured from multiple sources (XML/OCR/Manual).
    /// </summary>
    public List<RfcVariant> RfcVariantes { get; } = new();

    /// <summary>
    /// Gets or sets the CURP if available.
    /// </summary>
    public string Curp { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the birth date if available.
    /// </summary>
    public DateOnly? FechaNacimiento { get; set; }

    /// <summary>
    /// Gets or sets the relationship to the case.
    /// </summary>
    public string? Relacion { get; set; }

    /// <summary>
    /// Gets or sets the address.
    /// </summary>
    public string? Domicilio { get; set; }

    /// <summary>
    /// Gets or sets additional information (CURP, identifiers, etc.).
    /// </summary>
    public string? Complementarios { get; set; }

    /// <summary>
    /// Validation state for required fields.
    /// </summary>
    public ValidationState Validation { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonaSolicitud"/> class.
    /// </summary>
    public PersonaSolicitud()
    {
    }
}
