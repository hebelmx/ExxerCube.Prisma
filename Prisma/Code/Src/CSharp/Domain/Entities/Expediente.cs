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
    /// Initializes a new instance of the <see cref="Expediente"/> class.
    /// </summary>
    public Expediente()
    {
    }
}