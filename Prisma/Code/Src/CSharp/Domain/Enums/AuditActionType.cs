namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents the type of action being audited.
/// </summary>
public enum AuditActionType
{
    /// <summary>
    /// File download action.
    /// </summary>
    Download = 0,

    /// <summary>
    /// Document classification action.
    /// </summary>
    Classification = 1,

    /// <summary>
    /// File move/organization action.
    /// </summary>
    Move = 2,

    /// <summary>
    /// Metadata extraction action.
    /// </summary>
    Extraction = 3,

    /// <summary>
    /// Manual review action.
    /// </summary>
    Review = 4,

    /// <summary>
    /// Export generation action.
    /// </summary>
    Export = 5,

    /// <summary>
    /// SLA escalation action.
    /// </summary>
    Escalation = 6
}

