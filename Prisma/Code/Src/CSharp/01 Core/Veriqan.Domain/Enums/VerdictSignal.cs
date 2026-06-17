namespace ExxerCube.Prisma.Veriqan.Domain.Enums;

/// <summary>
/// Traffic-light signal that summarises all findings for a <see cref="Entities.VerificationJob"/>
/// into a single consumer-facing rollup.
/// </summary>
public enum VerdictSignal
{
    /// <summary>All checks passed — content is compliant.</summary>
    Green = 0,

    /// <summary>One or more checks failed — content is non-compliant.</summary>
    Red = 1,

    /// <summary>
    /// One or more checks returned <see cref="FindingVerdict.InsufficientData"/> and no check
    /// explicitly failed; manual review is required before a definitive ruling can be made.
    /// </summary>
    Blocked = 2,
}
