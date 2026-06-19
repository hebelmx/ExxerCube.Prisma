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
    /// Emitted by a whole-verdict <c>BlockedOutcome</c> (e.g. insufficient text layer or
    /// extraction-coverage floor) — processing was halted before a ruling could be made and
    /// manual review is required. Note: an aggregation of <see cref="FindingVerdict.InsufficientData"/>
    /// alone (with no explicit fail) correctly yields <see cref="Green"/>, not Blocked.
    /// </summary>
    Blocked = 2,
}
