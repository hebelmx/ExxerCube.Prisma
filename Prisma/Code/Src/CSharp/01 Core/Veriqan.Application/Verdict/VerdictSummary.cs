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
        IReadOnlyList<TenantDeviation>? tenantDeviations = null,
        IReadOnlyList<string>? legalBreachCheckIds = null,
        IReadOnlyList<string>? tenantOnlyFailCheckIds = null)
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
        LegalBreachCheckIds = legalBreachCheckIds ?? [];
        TenantOnlyFailCheckIds = tenantOnlyFailCheckIds ?? [];
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
    // Legal-baseline separable signal (Fix D — Story 9 remediation)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets the <see cref="Domain.Verification.RuleFinding.CheckId"/> values of findings where
    /// <see cref="Domain.Verification.RuleFinding.LegalBaselineVerdict"/> is
    /// <see cref="FindingVerdict.Fail"/> — i.e. the statement breaches the CONDUSEF legal floor,
    /// not merely the tenant's stricter bar.
    /// </summary>
    /// <remarks>
    /// Empty when <see cref="Signal"/> is <see cref="VerdictSignal.Blocked"/> or when no
    /// findings breach the legal floor.
    /// </remarks>
    public IReadOnlyList<string> LegalBreachCheckIds { get; }

    /// <summary>
    /// Gets the <see cref="Domain.Verification.RuleFinding.CheckId"/> values of findings where
    /// <see cref="Domain.Verification.RuleFinding.Verdict"/> is <see cref="FindingVerdict.Fail"/>
    /// <b>but</b> <see cref="Domain.Verification.RuleFinding.LegalBaselineVerdict"/> is
    /// <see cref="FindingVerdict.Pass"/> — i.e. the statement satisfies the legal floor but
    /// fails the tenant's stricter threshold.
    /// </summary>
    /// <remarks>
    /// A non-empty list here indicates the pipeline gate (on effective <c>Verdict</c>) fired
    /// for a purely tenant-driven reason, not a regulatory breach.
    /// </remarks>
    public IReadOnlyList<string> TenantOnlyFailCheckIds { get; }

    /// <summary>
    /// Gets a separable traffic-light signal derived exclusively from
    /// <see cref="LegalBreachCheckIds"/>: <see cref="VerdictSignal.Red"/> when at least one
    /// finding breaches the CONDUSEF legal floor; <see cref="VerdictSignal.Green"/> otherwise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This signal is purely informational — the pipeline gates on <see cref="Signal"/>
    /// (the effective, possibly-stricter tenant verdict) and this value must NOT change that.
    /// </para>
    /// <para>
    /// When <see cref="Signal"/> is <see cref="VerdictSignal.Blocked"/> this property is
    /// also <see cref="VerdictSignal.Blocked"/> to match the statement's overall state.
    /// </para>
    /// </remarks>
    public VerdictSignal LegalBaselineSignal =>
        Signal == VerdictSignal.Blocked
            ? VerdictSignal.Blocked
            : LegalBreachCheckIds.Count > 0
                ? VerdictSignal.Red
                : VerdictSignal.Green;

    // -----------------------------------------------------------------------
    // Static factories
    // -----------------------------------------------------------------------

    /// <summary>Creates a <see cref="VerdictSignal.Green"/> summary from aggregated counts.</summary>
    /// <param name="passCount">Count of passing findings.</param>
    /// <param name="insufficientDataCount">Count of InsufficientData findings.</param>
    /// <param name="insufficientDataCheckIds">Check IDs with InsufficientData verdicts.</param>
    /// <param name="tenantDeviations">Rejected tenant override deviations, if any.</param>
    /// <param name="legalBreachCheckIds">
    /// Check IDs whose <see cref="Domain.Verification.RuleFinding.LegalBaselineVerdict"/>
    /// is <see cref="FindingVerdict.Fail"/>. Pass <see langword="null"/> or omit when there
    /// are none (no legal-floor breaches in a Green summary).
    /// </param>
    /// <param name="tenantOnlyFailCheckIds">
    /// Check IDs where <see cref="Domain.Verification.RuleFinding.Verdict"/> is Fail but
    /// <see cref="Domain.Verification.RuleFinding.LegalBaselineVerdict"/> is Pass.
    /// Pass <see langword="null"/> or omit when there are none.
    /// </param>
    internal static VerdictSummary Green(
        int passCount,
        int insufficientDataCount,
        IReadOnlyList<string> insufficientDataCheckIds,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null,
        IReadOnlyList<string>? legalBreachCheckIds = null,
        IReadOnlyList<string>? tenantOnlyFailCheckIds = null) =>
        new(
            signal: VerdictSignal.Green,
            failCount: 0,
            passCount: passCount,
            insufficientDataCount: insufficientDataCount,
            total: passCount + insufficientDataCount,
            failCheckIds: [],
            insufficientDataCheckIds: insufficientDataCheckIds,
            blockedOutcome: null,
            tenantDeviations: tenantDeviations,
            legalBreachCheckIds: legalBreachCheckIds,
            tenantOnlyFailCheckIds: tenantOnlyFailCheckIds);

    /// <summary>Creates a <see cref="VerdictSignal.Red"/> summary from aggregated counts.</summary>
    /// <param name="failCount">Count of failing findings.</param>
    /// <param name="passCount">Count of passing findings.</param>
    /// <param name="insufficientDataCount">Count of InsufficientData findings.</param>
    /// <param name="failCheckIds">Check IDs with Fail verdicts.</param>
    /// <param name="insufficientDataCheckIds">Check IDs with InsufficientData verdicts.</param>
    /// <param name="tenantDeviations">Rejected tenant override deviations, if any.</param>
    /// <param name="legalBreachCheckIds">
    /// Check IDs whose <see cref="Domain.Verification.RuleFinding.LegalBaselineVerdict"/>
    /// is <see cref="FindingVerdict.Fail"/>. Pass <see langword="null"/> or omit when there
    /// are none.
    /// </param>
    /// <param name="tenantOnlyFailCheckIds">
    /// Check IDs where effective <see cref="Domain.Verification.RuleFinding.Verdict"/> is Fail
    /// but <see cref="Domain.Verification.RuleFinding.LegalBaselineVerdict"/> is Pass.
    /// Pass <see langword="null"/> or omit when there are none.
    /// </param>
    internal static VerdictSummary Red(
        int failCount,
        int passCount,
        int insufficientDataCount,
        IReadOnlyList<string> failCheckIds,
        IReadOnlyList<string> insufficientDataCheckIds,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null,
        IReadOnlyList<string>? legalBreachCheckIds = null,
        IReadOnlyList<string>? tenantOnlyFailCheckIds = null) =>
        new(
            signal: VerdictSignal.Red,
            failCount: failCount,
            passCount: passCount,
            insufficientDataCount: insufficientDataCount,
            total: failCount + passCount + insufficientDataCount,
            failCheckIds: failCheckIds,
            insufficientDataCheckIds: insufficientDataCheckIds,
            blockedOutcome: null,
            tenantDeviations: tenantDeviations,
            legalBreachCheckIds: legalBreachCheckIds,
            tenantOnlyFailCheckIds: tenantOnlyFailCheckIds);

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
            tenantDeviations: tenantDeviations,
            legalBreachCheckIds: null,
            tenantOnlyFailCheckIds: null);

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
