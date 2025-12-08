namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents a summary of compliance requirements extracted from PDF documents.
/// </summary>
public class RequirementSummary
{
    /// <summary>
    /// Gets or sets the list of compliance requirements extracted from the PDF.
    /// </summary>
    public List<ComplianceRequirement> Requirements { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of bloqueo (block) requirements.
    /// </summary>
    public List<ComplianceRequirement> Bloqueo { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of desbloqueo (unblock) requirements.
    /// </summary>
    public List<ComplianceRequirement> Desbloqueo { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of documentacion (documentation) requirements.
    /// </summary>
    public List<ComplianceRequirement> Documentacion { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of transferencia (transfer) requirements.
    /// </summary>
    public List<ComplianceRequirement> Transferencia { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of informacion (information) requirements.
    /// </summary>
    public List<ComplianceRequirement> Informacion { get; set; } = new();

    /// <summary>
    /// Gets or sets the human-readable summary text.
    /// </summary>
    public string SummaryText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the extraction date and time.
    /// </summary>
    public DateTime ExtractedAt { get; set; }

    /// <summary>
    /// Gets or sets the confidence score for the extraction (0-100).
    /// </summary>
    public int ConfidenceScore { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the extraction process.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirementSummary"/> class.
    /// </summary>
    public RequirementSummary()
    {
        ExtractedAt = DateTime.UtcNow;
    }
}

