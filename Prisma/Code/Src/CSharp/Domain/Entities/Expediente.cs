using ExxerCube.Prisma.Domain.Enums;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents a regulatory case file (expediente) from CNBV/UIF.
/// </summary>
public class Expediente
{
    /// <summary>
    /// Gets or sets the case number (e.g., "A/AS1-2505-088637-PHM").
    /// </summary>
    public string NumeroExpediente { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the oficio number (e.g., "214-1-18714972/2025").
    /// </summary>
    public string NumeroOficio { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SIARA request number.
    /// </summary>
    public string SolicitudSiara { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the folio number.
    /// </summary>
    public int Folio { get; set; }

    /// <summary>
    /// Gets or sets the year of the oficio.
    /// </summary>
    public int OficioYear { get; set; }

    /// <summary>
    /// Gets or sets the area code.
    /// </summary>
    public int AreaClave { get; set; }

    /// <summary>
    /// Gets or sets the area description (e.g., "ASEGURAMIENTO", "HACENDARIO").
    /// </summary>
    public string AreaDescripcion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the controlled subdivision code.
    /// </summary>
    public LegalSubdivision Subdivision { get; set; } = LegalSubdivision.Unknown;

    /// <summary>
    /// Gets or sets the publication date.
    /// </summary>
    public DateTime FechaPublicacion { get; set; }

    /// <summary>
    /// Gets or sets the days granted for compliance.
    /// </summary>
    public int DiasPlazo { get; set; }

    /// <summary>
    /// Gets or sets the authority name.
    /// </summary>
    public string AutoridadNombre { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the specific authority name (nullable).
    /// </summary>
    public string? AutoridadEspecificaNombre { get; set; }

    /// <summary>
    /// Gets or sets the requester name (nullable).
    /// </summary>
    public string? NombreSolicitante { get; set; }

    /// <summary>
    /// Gets or sets the legal basis.
    /// </summary>
    public string FundamentoLegal { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the delivery channel (SIARA/Fisico).
    /// </summary>
    public string MedioEnvio { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets evidence of signature or submission (hash/ticket).
    /// </summary>
    public string EvidenciaFirma { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the originating oficio identifier (if referenced).
    /// </summary>
    public string OficioOrigen { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the referenced legal agreement (e.g., 105/2021).
    /// </summary>
    public string AcuerdoReferencia { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reference field.
    /// </summary>
    public string Referencia { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the first additional reference field.
    /// </summary>
    public string Referencia1 { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the second additional reference field.
    /// </summary>
    public string Referencia2 { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the case involves asset seizure.
    /// </summary>
    public bool TieneAseguramiento { get; set; }

    /// <summary>
    /// Gets or sets the list of parties involved.
    /// </summary>
    public List<SolicitudParte> SolicitudPartes { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of specific requests.
    /// </summary>
    public List<SolicitudEspecifica> SolicitudEspecificas { get; set; } = new();

    /// <summary>
    /// Fecha de recepcion del documento
    /// </summary>
    public DateTime FechaRecepcion { get; set; }

    /// <summary>
    /// Fecha de registro del documento.
    /// </summary>
    public DateTime FechaRegistro { get; set; }

    /// <summary>
    /// Fecha estimada de conclusión (recepción + días hábiles).
    /// </summary>
    public DateTime FechaEstimadaConclusion { get; set; }

    /// <summary>
    /// Validation state for required fields.
    /// </summary>
    public ValidationState Validation { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Expediente"/> class.
    /// </summary>
    public Expediente()
    {
    }
}
