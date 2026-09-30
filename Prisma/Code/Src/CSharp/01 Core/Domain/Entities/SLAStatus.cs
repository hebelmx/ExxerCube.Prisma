namespace ExxerCube.Prisma.Domain.Entities;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents SLA deadline tracking and escalation status for regulatory response cases.
/// </summary>
public class SLAStatus
{
    /// <summary>
    /// Gets or sets the file identifier (foreign key to FileMetadata.FileId).
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date when the file was received (intake date).
    /// </summary>
    public DateTime IntakeDate { get; set; }

    /// <summary>
    /// Gets or sets the calculated deadline (intake date + business days).
    /// </summary>
    public DateTime Deadline { get; set; }

    /// <summary>
    /// Gets or sets the number of days granted for compliance (dias plazo).
    /// </summary>
    public int DaysPlazo { get; set; }

    /// <summary>
    /// Gets or sets the time remaining until deadline.
    /// </summary>
    public TimeSpan RemainingTime { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the case is within critical threshold.
    /// </summary>
    public bool IsAtRisk { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the deadline has been breached.
    /// </summary>
    public bool IsBreached { get; set; }

    /// <summary>
    /// Gets or sets the current escalation level.
    /// </summary>
    public EscalationLevel EscalationLevel { get; set; } = EscalationLevel.None;

    /// <summary>
    /// Gets or sets the timestamp when escalation was triggered (nullable).
    /// </summary>
    public DateTime? EscalatedAt { get; set; }
}

