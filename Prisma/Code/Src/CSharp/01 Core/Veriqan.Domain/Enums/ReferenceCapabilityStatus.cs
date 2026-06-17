namespace ExxerCube.Prisma.Veriqan.Domain.Enums;

/// <summary>
/// Indicates whether a <see cref="ReferenceCapability"/> is available for use by
/// checklist checks in a given verification run.
/// </summary>
public enum ReferenceCapabilityStatus
{
    /// <summary>
    /// The reference-data section backing this capability is present and non-empty.
    /// Checks that depend on this capability may proceed normally.
    /// </summary>
    Available = 0,

    /// <summary>
    /// The reference-data section backing this capability is absent or empty in the bundle.
    /// Checks that depend on this capability must emit <c>INSUFFICIENT_DATA</c>
    /// rather than producing a verdicted finding.
    /// </summary>
    InsufficientData = 1,
}
