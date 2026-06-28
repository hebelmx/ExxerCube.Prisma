using System.Collections.Generic;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Verdict;

/// <summary>
/// Aggregates a collection of <see cref="RuleFinding"/> items — together with an optional
/// upstream <see cref="BlockedOutcome"/> — into a single <see cref="VerdictSummary"/> (FR-15).
/// </summary>
/// <remarks>
/// <para>
/// <b>Precedence:</b>
/// <list type="number">
///   <item>If <c>blocked</c> is non-null the summary is <c>BLOCKED</c> regardless of findings.</item>
///   <item>If any finding is <see cref="Domain.Enums.FindingVerdict.Fail"/> the summary is <c>RED</c>.</item>
///   <item>Otherwise the summary is <c>GREEN</c>.
///     <see cref="Domain.Enums.FindingVerdict.InsufficientData"/> findings do <b>not</b> escalate to Red;
///     they are surfaced separately via <see cref="VerdictSummary.InsufficientDataCount"/> and
///     <see cref="VerdictSummary.InsufficientDataCheckIds"/>.</item>
/// </list>
/// </para>
/// <para>
/// An empty findings list with no blocking outcome yields <c>GREEN</c>
/// with all counts at zero — callers that require at least one finding to declare Green should
/// validate the collection before calling.
/// </para>
/// </remarks>
public interface IVerdictAggregator
{
    /// <summary>
    /// Aggregates <paramref name="findings"/> into a <see cref="VerdictSummary"/>.
    /// </summary>
    /// <param name="findings">
    /// The complete, deterministically-ordered list of rule findings produced by
    /// <see cref="Validation.IVecValidationEngine.RunAsync"/>.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <param name="blocked">
    /// Optional binding-level block that occurred before any rules ran.
    /// When non-null the returned summary always carries
    /// <see cref="Domain.Enums.VerdictSignal.Blocked"/> regardless of findings.
    /// </param>
    /// <param name="ct">
    /// Propagated cancellation token.  Returns a cancelled <c>Result</c> immediately
    /// when cancellation is already requested on entry.
    /// </param>
    /// <param name="tenantDeviations">
    /// Optional list of rejected tenant override deviations produced during tenant-profile
    /// resolution (Story 9.3b). Pass <see langword="null"/> (or omit) when no tenant profile
    /// was applied; treated as an empty list in the resulting <see cref="VerdictSummary"/>.
    /// </param>
    /// <param name="checklistTiers">
    /// Optional per-tenant tier membership map (Story 1.2 / 1.3), keyed by
    /// <see cref="RuleFinding.CheckId"/>, value is the <see cref="ChecklistTier"/> for that check.
    /// <para>
    /// When <see langword="null"/> (default) the aggregator uses the <b>legacy single-tier path</b>:
    /// every fail is treated as CONDUSEF-mandated; <see cref="VerdictSummary.BankTierVerdict"/>
    /// is Green; <see cref="VerdictSummary.CondusefTierVerdict"/> mirrors the overall signal.
    /// Existing call sites that omit this parameter are guaranteed byte-for-byte legacy behavior.
    /// </para>
    /// <para>
    /// When non-null the aggregator partitions fail findings by tier, computing
    /// <see cref="VerdictSummary.BankTierVerdict"/>, <see cref="VerdictSummary.CondusefTierVerdict"/>,
    /// <see cref="VerdictSummary.BankFailCheckIds"/>, and <see cref="VerdictSummary.CondusefFailCheckIds"/>.
    /// The overall <see cref="VerdictSummary.Signal"/> is computed via
    /// <see cref="VerdictSummary.CombineOverallSignal"/> so it may differ from the legacy path
    /// (e.g. bank-only fails yield Yellow instead of Red).
    /// </para>
    /// <para>
    /// A <see cref="RuleFinding.CheckId"/> absent from the map is conservatively treated as
    /// <see cref="ChecklistTier.Condusef"/> so no new rule is silently dropped from a RED outcome.
    /// </para>
    /// <para>
    /// <see cref="Domain.Enums.FindingVerdict.InsufficientData"/> findings never escalate to a tier
    /// fail (abstain-safety) regardless of tier membership.
    /// </para>
    /// </param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> wrapping a <see cref="VerdictSummary"/>;
    /// a cancelled result if <paramref name="ct"/> was cancelled; or a failure result
    /// for unexpected errors (e.g. null argument).
    /// </returns>
    Result<VerdictSummary> Aggregate(
        IReadOnlyList<RuleFinding> findings,
        BlockedOutcome? blocked = null,
        CancellationToken ct = default,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null,
        IReadOnlyDictionary<string, ChecklistTier>? checklistTiers = null);
}
