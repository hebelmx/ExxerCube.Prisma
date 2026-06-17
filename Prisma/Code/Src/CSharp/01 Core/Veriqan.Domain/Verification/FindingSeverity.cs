namespace ExxerCube.Prisma.Veriqan.Domain.Verification;

/// <summary>
/// Characterises how severe a rule finding is, independently of its <see cref="Enums.FindingVerdict"/>.
/// A <c>Pass</c> verdict always carries <see cref="Info"/>; the severity levels apply mainly
/// to <c>Fail</c> and <c>InsufficientData</c> verdicts to allow downstream triage.
/// </summary>
/// <remarks>
/// Severity is declared by each rule author and represents the business impact of a failure.
/// <list type="bullet">
///   <item><see cref="Info"/> — informational; does not block acceptance.</item>
///   <item><see cref="Warning"/> — notable deviation; should be reviewed but not blocking.</item>
///   <item><see cref="Critical"/> — compliance-blocking deviation; must be resolved before acceptance.</item>
/// </list>
/// </remarks>
public enum FindingSeverity
{
    /// <summary>
    /// Informational finding.  Passes and advisory observations use this level.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Notable deviation that warrants human review but does not block acceptance on its own.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Compliance-blocking deviation.  A single <see cref="Critical"/> finding should
    /// cause the overall verification to be flagged for mandatory review.
    /// </summary>
    Critical = 2,
}
