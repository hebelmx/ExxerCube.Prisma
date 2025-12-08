using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Domain.Enum;

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
    public LegalSubdivisionKind Subdivision { get; set; } = LegalSubdivisionKind.Unknown;

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
    /// CNBV law-mandated fields for R29 compliance.
    /// Nullable until CNBV XML schema includes these fields, bank systems provide data,
    /// or manual enrichment occurs.
    /// </summary>
    /// <remarks>
    /// Source: Laws/MandatoryFields_CNBV.md, DATA_MODEL.md Sections 2.1-2.4
    /// Contains fields required by law but not yet in XML samples (will be null initially).
    /// Expediente is the ubiquitous language - natural place for case data (DDD).
    /// </remarks>
    public LawMandatedFields? LawMandatedFields { get; set; }

    /// <summary>
    /// Semantic analysis of legal directive - the "5 Situations".
    /// Describes WHAT the case requires (bloqueo, desbloqueo, documentación, etc.).
    /// Nullable until semantic analysis is performed by classification engine.
    /// </summary>
    /// <remarks>
    /// Source: DATA_MODEL.md Section 2.5
    /// This is a DOMAIN field (what the case requires) computed by infrastructure.
    /// Once computed, it becomes a fact about the expediente.
    /// </remarks>
    public SemanticAnalysis? SemanticAnalysis { get; set; }

    /// <summary>
    /// Captures any XML fields not recognized by current schema.
    /// Future-proofing for CNBV schema evolution - ensures zero data loss.
    /// </summary>
    /// <remarks>
    /// When CNBV adds new fields to XML, they are captured here automatically.
    /// Extractor logs warnings for unknown fields but preserves data.
    /// Defensive intelligence pattern.
    /// </remarks>
    public Dictionary<string, string> AdditionalFields { get; set; } = new();

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
