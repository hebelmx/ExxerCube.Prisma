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

    /// <summary>
    /// The statement is acceptable at the bank's own bar but has non-blocking improvement
    /// opportunities relative to the bank's full ruleset (bank-tier passes with gaps).
    /// <para>
    /// Two-tier combination rule (Story 1.1):
    /// <list type="bullet">
    ///   <item><see cref="Blocked"/> — takes absolute precedence over both tier verdicts.</item>
    ///   <item><see cref="Red"/> overall — when the CONDUSEF tier verdict is <see cref="Red"/>.</item>
    ///   <item><see cref="Yellow"/> overall — when the bank tier verdict is <see cref="Yellow"/>
    ///     and the CONDUSEF tier verdict is not <see cref="Red"/>.</item>
    ///   <item><see cref="Green"/> overall — both tiers are <see cref="Green"/>.</item>
    /// </list>
    /// </para>
    /// <para>
    /// See <c>VerdictSummary.BankTierVerdict</c>, <c>VerdictSummary.CondusefTierVerdict</c>, and
    /// <c>VerdictSummary.CombineOverallSignal</c> in <c>Veriqan.Application</c>.
    /// </para>
    /// </summary>
    Yellow = 3,
}
