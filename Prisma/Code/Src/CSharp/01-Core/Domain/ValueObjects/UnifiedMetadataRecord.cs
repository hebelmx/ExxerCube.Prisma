namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Consolidated metadata record combining data from XML, DOCX, and PDF sources.
/// </summary>
public class UnifiedMetadataRecord
{
    /// <summary>
    /// Gets or sets the regulatory case information (expediente).
    /// </summary>
    public Expediente? Expediente { get; set; }

    /// <summary>
    /// Gets or sets the list of persons involved (canonical personas from XML/PDF/OCR).
    /// </summary>
    public List<PersonaSolicitud> Personas { get; set; } = new();

    /// <summary>
    /// Gets or sets the regulatory directive information (oficio).
    /// </summary>
    public Oficio? Oficio { get; set; }

    /// <summary>
    /// Gets or sets the field extraction results (extends existing entity).
    /// </summary>
    public ExtractedFields? ExtractedFields { get; set; }

    /// <summary>
    /// Gets or sets the document classification result.
    /// </summary>
    public ClassificationResult? Classification { get; set; }

    /// <summary>
    /// Gets or sets the field matching results across sources.
    /// </summary>
    public MatchedFields? MatchedFields { get; set; }

    /// <summary>
    /// Gets or sets the merged additional fields (from XML/OCR) and conflicts.
    /// </summary>
    public Dictionary<string, string?> AdditionalFields { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of additional field names with conflicts.
    /// </summary>
    public List<string> AdditionalFieldConflicts { get; set; } = new();

    /// <summary>
    /// Gets or sets the PDF requirement summary.
    /// </summary>
    public RequirementSummary? RequirementSummary { get; set; }

    /// <summary>
    /// Gets or sets the SLA tracking information.
    /// </summary>
    public SLAStatus? SlaStatus { get; set; }

    /// <summary>
    /// Gets or sets the persona information (legacy single persona entry, if needed).
    /// </summary>
    public Persona? Persona { get; set; }

    /// <summary>
    /// Gets or sets the list of compliance actions.
    /// </summary>
    public List<ComplianceAction> ComplianceActions { get; set; } = new();

    /// <summary>
    /// Gets or sets the validation state (warnings/missing fields) for the consolidated record.
    /// </summary>
    public ValidationState Validation { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="UnifiedMetadataRecord"/> class.
    /// </summary>
    public UnifiedMetadataRecord()
    {
    }
}

