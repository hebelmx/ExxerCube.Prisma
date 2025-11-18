namespace ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Represents the type of decision made during manual review.
/// </summary>
public enum DecisionType
{
    /// <summary>
    /// Approve - reviewer approves the classification or extraction and it can proceed.
    /// </summary>
    Approve,

    /// <summary>
    /// Reject - reviewer rejects the classification or extraction.
    /// </summary>
    Reject,

    /// <summary>
    /// RequestMoreInfo - reviewer requests additional information before making a decision.
    /// </summary>
    RequestMoreInfo
}

