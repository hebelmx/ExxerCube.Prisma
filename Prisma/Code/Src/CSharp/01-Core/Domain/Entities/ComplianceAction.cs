using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents a compliance action to be taken based on legal directive classification.
/// </summary>
public class ComplianceAction
{
    /// <summary>
    /// Gets or sets the action type (Block, Unblock, Document, Transfer, Information, Ignore).
    /// </summary>
    public ComplianceActionKind ActionType { get; set; } = ComplianceActionKind.Unknown;

    /// <summary>
    /// Gets or sets the account number if applicable.
    /// </summary>
    public string? AccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the structured account reference if applicable.
    /// </summary>
    public Cuenta? Cuenta { get; set; }

    /// <summary>
    /// Gets or sets the product type if applicable.
    /// </summary>
    public string? ProductType { get; set; }

    /// <summary>
    /// Gets or sets the amount if applicable.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the original expediente that required action.
    /// </summary>
    public string? ExpedienteOrigen { get; set; }

    /// <summary>
    /// Gets or sets the original oficio that required action.
    /// </summary>
    public string? OficioOrigen { get; set; }

    /// <summary>
    /// Gets or sets the original requerimiento ID.
    /// </summary>
    public string? RequerimientoOrigen { get; set; }

    /// <summary>
    /// Gets or sets additional action-specific data.
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();

    /// <summary>
    /// Gets or sets the confidence score for this compliance action (0-100).
    /// </summary>
    public int Confidence { get; set; }

    /// <summary>
    /// Gets or sets the list of warnings detected during classification.
    /// Used for flagging edge cases and potential issues requiring manual review.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this action requires manual review.
    /// Set to true when confidence is low, required data is missing, or edge cases are detected.
    /// </summary>
    public bool RequiresManualReview { get; set; }

    /// <summary>
    /// Gets or sets the document relation type (NewRequirement, Recordatorio, Alcance, Precisión).
    /// Used to determine how to process the document in relation to existing requirements.
    /// </summary>
    public DocumentRelationType DocumentRelationType { get; set; } = DocumentRelationType.NewRequirement;

    /// <summary>
    /// Legal basis for the preocedure
    /// </summary>
    public string LegalBasis { get; set; } = string.Empty;

    /// <summary>
    /// DueDate to enforce the action
    /// </summary>
    public DateTime DueDate { get; set; }

    /// <summary>
    /// Validation state for required fields.
    /// </summary>
    public ValidationState Validation { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceAction"/> class.
    /// </summary>
    public ComplianceAction()
    {
    }
}
