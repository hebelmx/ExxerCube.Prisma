namespace ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Represents the escalation level for SLA tracking.
/// </summary>
public enum EscalationLevel
{
    /// <summary>
    /// No escalation - case is within normal SLA parameters.
    /// </summary>
    None = 0,

    /// <summary>
    /// Warning level - case is approaching deadline (typically less than  24 hours remaining).
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Critical level - case is at high risk (typically less than 4 hours remaining).
    /// </summary>
    Critical = 2,

    /// <summary>
    /// Breached level - deadline has passed.
    /// </summary>
    Breached = 3
}