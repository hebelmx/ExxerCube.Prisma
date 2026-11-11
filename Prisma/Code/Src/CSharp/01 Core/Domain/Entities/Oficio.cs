using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents a regulatory directive (oficio).
/// </summary>
public class Oficio
{
    /// <summary>
    /// Gets or sets the oficio number.
    /// </summary>
    public string NumeroOficio { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the related expediente number.
    /// </summary>
    public string NumeroExpediente { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the receipt date.
    /// </summary>
    public DateTime FechaRecepcion { get; set; }

    /// <summary>
    /// Gets or sets the registration date.
    /// </summary>
    public DateTime FechaRegistro { get; set; }

    /// <summary>
    /// Gets or sets the estimated completion date.
    /// </summary>
    public DateTime FechaEstimadaConclusion { get; set; }

    /// <summary>
    /// Gets or sets the days granted for compliance.
    /// </summary>
    public int DiasPlazo { get; set; }

    /// <summary>
    /// Gets or sets the subject type ("EMBARGO", "DESEMBARGO", "DOCUMENTACIÓN", etc.).
    /// </summary>
    public string TipoAsunto { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subdivision (controlled code).
    /// </summary>
    public LegalSubdivisionKind Subdivision { get; set; } = LegalSubdivisionKind.Unknown;

    /// <summary>
    /// Gets or sets the full description.
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender name.
    /// </summary>
    public string NombreRemitente { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of compliance requirements.
    /// </summary>
    public List<ComplianceRequirement> Requisitos { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Oficio"/> class.
    /// </summary>
    public Oficio()
    {
    }
}

