using System.Collections.Generic;
using System.Linq;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;

namespace ExxerCube.Prisma.Veriqan.Application.Verdict;

/// <summary>
/// Immutable value object that summarises a complete set of <see cref="Domain.Verification.RuleFinding"/>
/// items into a single traffic-light signal together with per-category counts and diagnostic lists.
/// </summary>
/// <remarks>
/// <para>
/// <b>Precedence (FR-15):</b>
/// <list type="number">
///   <item><b>BLOCKED</b> — a <see cref="BlockedOutcome"/> was supplied; binding could not
///     complete before rules ran.  The signal is <see cref="VerdictSignal.Blocked"/>
///     regardless of findings.</item>
///   <item><b>RED</b> — at least one finding has <see cref="FindingVerdict.Fail"/>.
///     The signal is <see cref="VerdictSignal.Red"/>.</item>
///   <item><b>GREEN</b> — all findings are Pass or InsufficientData (or the list is empty).
///     <see cref="FindingVerdict.InsufficientData"/> findings do <b>not</b> escalate to Red;
///     they are surfaced separately via <see cref="InsufficientDataCount"/> and
///     <see cref="InsufficientDataCheckIds"/>.</item>
/// </list>
/// </para>
/// <para>
/// <b>Mapping to <see cref="Domain.Entities.JobVerdict"/>:</b>
/// Use <see cref="ToJobVerdict"/> when the persistence story (Epic 7, Story 7.4) needs to
/// materialise a <see cref="Domain.Entities.JobVerdict"/> entity from this summary.
/// </para>
/// </remarks>
public sealed record VerdictSummary
{
    // -----------------------------------------------------------------------
    // Construction (via static factories only — keeps ctor args hidden)
    // -----------------------------------------------------------------------

    private VerdictSummary(
        VerdictSignal signal,
        int failCount,
        int passCount,
        int insufficientDataCount,
        int total,
        IReadOnlyList<string> failCheckIds,
        IReadOnlyList<string> insufficientDataCheckIds,
        BlockedOutcome? blockedOutcome,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null)
    {
        Signal = signal;
        FailCount = failCount;
        PassCount = passCount;
        InsufficientDataCount = insufficientDataCount;
        Total = total;
        FailCheckIds = failCheckIds;
        InsufficientDataCheckIds = insufficientDataCheckIds;
        BlockedOutcome = blockedOutcome;
        TenantDeviations = tenantDeviations ?? [];
    }

    // -----------------------------------------------------------------------
    // Properties
    // -----------------------------------------------------------------------

    /// <summary>Gets the traffic-light signal that summarises all findings.</summary>
    public VerdictSignal Signal { get; }

    /// <summary>Gets the count of <see cref="FindingVerdict.Fail"/> findings.</summary>
    public int FailCount { get; }

    /// <summary>Gets the count of <see cref="FindingVerdict.Pass"/> findings.</summary>
    public int PassCount { get; }

    /// <summary>
    /// Gets the count of <see cref="FindingVerdict.InsufficientData"/> findings.
    /// These are reported here but do <b>not</b> by themselves make the signal Red.
    /// </summary>
    public int InsufficientDataCount { get; }

    /// <summary>Gets the total number of findings evaluated (Pass + Fail + InsufficientData).</summary>
    public int Total { get; }

    /// <summary>Gets the <see cref="Domain.Verification.RuleFinding.CheckId"/> values of all failed checks.</summary>
    public IReadOnlyList<string> FailCheckIds { get; }

    /// <summary>
    /// Gets the <see cref="Domain.Verification.RuleFinding.CheckId"/> values of all InsufficientData checks.
    /// </summary>
    public IReadOnlyList<string> InsufficientDataCheckIds { get; }

    /// <summary>
    /// Gets the <see cref="Binding.BlockedOutcome"/> when <see cref="Signal"/> is
    /// <see cref="VerdictSignal.Blocked"/>; otherwise <see langword="null"/>.
    /// </summary>
    public BlockedOutcome? BlockedOutcome { get; }

    /// <summary>
    /// Gets the list of rejected tenant override deviations produced during tenant-profile
    /// resolution (Story 9.3b). Empty when no tenant profile was applied or when all
    /// overrides were accepted.
    /// </summary>
    /// <remarks>
    /// Surfaced here so that compliance reviewers examining the verdict summary can see
    /// which tenant tolerance overrides were silently reverted to the CONDUSEF legal baseline.
    /// </remarks>
    public IReadOnlyList<TenantDeviation> TenantDeviations { get; }

    // -----------------------------------------------------------------------
    // Static factories
    // -----------------------------------------------------------------------

    /// <summary>Creates a <see cref="VerdictSignal.Green"/> summary from aggregated counts.</summary>
    /// <param name="passCount">Count of passing findings.</param>
    /// <param name="insufficientDataCount">Count of InsufficientData findings.</param>
    /// <param name="insufficientDataCheckIds">Check IDs with InsufficientData verdicts.</param>
    /// <param name="tenantDeviations">Rejected tenant override deviations, if any.</param>
    internal static VerdictSummary Green(
        int passCount,
        int insufficientDataCount,
        IReadOnlyList<string> insufficientDataCheckIds,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null) =>
        new(
            signal: VerdictSignal.Green,
            failCount: 0,
            passCount: passCount,
            insufficientDataCount: insufficientDataCount,
            total: passCount + insufficientDataCount,
            failCheckIds: [],
            insufficientDataCheckIds: insufficientDataCheckIds,
            blockedOutcome: null,
            tenantDeviations: tenantDeviations);

    /// <summary>Creates a <see cref="VerdictSignal.Red"/> summary from aggregated counts.</summary>
    /// <param name="failCount">Count of failing findings.</param>
    /// <param name="passCount">Count of passing findings.</param>
    /// <param name="insufficientDataCount">Count of InsufficientData findings.</param>
    /// <param name="failCheckIds">Check IDs with Fail verdicts.</param>
    /// <param name="insufficientDataCheckIds">Check IDs with InsufficientData verdicts.</param>
    /// <param name="tenantDeviations">Rejected tenant override deviations, if any.</param>
    internal static VerdictSummary Red(
        int failCount,
        int passCount,
        int insufficientDataCount,
        IReadOnlyList<string> failCheckIds,
        IReadOnlyList<string> insufficientDataCheckIds,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null) =>
        new(
            signal: VerdictSignal.Red,
            failCount: failCount,
            passCount: passCount,
            insufficientDataCount: insufficientDataCount,
            total: failCount + passCount + insufficientDataCount,
            failCheckIds: failCheckIds,
            insufficientDataCheckIds: insufficientDataCheckIds,
            blockedOutcome: null,
            tenantDeviations: tenantDeviations);

    /// <summary>Creates a <see cref="VerdictSignal.Blocked"/> summary from a binding failure.</summary>
    /// <param name="blockedOutcome">The blocking outcome from the binder.</param>
    /// <param name="tenantDeviations">Rejected tenant override deviations, if any.</param>
    internal static VerdictSummary Blocked(
        BlockedOutcome blockedOutcome,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null) =>
        new(
            signal: VerdictSignal.Blocked,
            failCount: 0,
            passCount: 0,
            insufficientDataCount: 0,
            total: 0,
            failCheckIds: [],
            insufficientDataCheckIds: [],
            blockedOutcome: blockedOutcome,
            tenantDeviations: tenantDeviations);

    // -----------------------------------------------------------------------
    // Projection
    // -----------------------------------------------------------------------

    /// <summary>
    /// Projects this summary onto a new <see cref="Domain.Entities.JobVerdict"/> entity.
    /// </summary>
    /// <remarks>
    /// Persistence mapping is deferred to Story 7.4; this convenience method is provided
    /// so callers can build the entity without knowing the internal mapping.
    /// </remarks>
    /// <param name="id">Unique identifier for the new <see cref="Domain.Entities.JobVerdict"/>.</param>
    /// <param name="verificationJobId">Parent job identifier.</param>
    /// <returns>A new <see cref="Domain.Entities.JobVerdict"/> with <see cref="Signal"/> wired in.</returns>
    public Domain.Entities.JobVerdict ToJobVerdict(System.Guid id, System.Guid verificationJobId) =>
        new(id, verificationJobId, Signal);
}
