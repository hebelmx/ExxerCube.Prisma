namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents an immutable audit log entry for all processing steps.
/// </summary>
public class AuditRecord
{
    /// <summary>
    /// Gets or sets the unique audit record identifier.
    /// </summary>
    public string AuditId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the correlation ID for tracking requests across processing stages.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file identifier (nullable if not applicable).
    /// </summary>
    public string? FileId { get; set; }

    /// <summary>
    /// Gets or sets the type of action being audited.
    /// </summary>
    public AuditActionType ActionType { get; set; } = AuditActionType.Unknown;

    /// <summary>
    /// Gets or sets the JSON serialized action details (nullable).
    /// </summary>
    public string? ActionDetails { get; set; }

    /// <summary>
    /// Gets or sets the user ID who performed the action (nullable for system actions).
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the action occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the processing stage where the action occurred.
    /// </summary>
    public ProcessingStage Stage { get; set; } = ProcessingStage.Unknown;

    /// <summary>
    /// Gets or sets a value indicating whether the action succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if action failed (nullable).
    /// </summary>
    public string? ErrorMessage { get; set; }
}

