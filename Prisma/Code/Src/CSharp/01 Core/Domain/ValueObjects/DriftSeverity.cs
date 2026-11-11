namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Severity level for schema drift.
/// </summary>
public enum DriftSeverity
{
    /// <summary>
    /// No drift detected.
    /// </summary>
    None = 0,

    /// <summary>
    /// Low severity - only new optional fields.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium severity - renamed fields or missing optional fields.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High severity - required fields missing.
    /// </summary>
    High = 3
}