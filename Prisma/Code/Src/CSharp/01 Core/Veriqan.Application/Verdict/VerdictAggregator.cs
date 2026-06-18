using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Application.Verdict;

/// <summary>
/// Default implementation of <see cref="IVerdictAggregator"/> (FR-15).
/// Pure deterministic logic — no I/O, no mutable state, no exceptions for control flow.
/// </summary>
/// <remarks>
/// <para>
/// <b>Precedence:</b>
/// <list type="number">
///   <item>BLOCKED — a binding-level <c>BlockedOutcome</c> was supplied.</item>
///   <item>RED    — any finding has <see cref="FindingVerdict.Fail"/>.</item>
///   <item>GREEN  — all findings are Pass or InsufficientData (or none exist).</item>
/// </list>
/// </para>
/// <para>
/// <see cref="FindingVerdict.InsufficientData"/> findings are <b>never</b> escalated to Red.
/// They are reported separately through <see cref="VerdictSummary.InsufficientDataCheckIds"/>
/// and <see cref="VerdictSummary.InsufficientDataCount"/>.
/// </para>
/// <para>
/// <b>Legal-baseline separability (Fix D — Story 9 remediation):</b>
/// In addition to the effective <see cref="VerdictSummary.Signal"/> (gated on
/// <see cref="RuleFinding.Verdict"/>), the aggregator computes:
/// <list type="bullet">
///   <item><see cref="VerdictSummary.LegalBreachCheckIds"/> — checks whose
///     <see cref="RuleFinding.LegalBaselineVerdict"/> is Fail.</item>
///   <item><see cref="VerdictSummary.TenantOnlyFailCheckIds"/> — checks where effective
///     Verdict is Fail but LegalBaselineVerdict is Pass (purely tenant-driven failures).</item>
///   <item><see cref="VerdictSummary.LegalBaselineSignal"/> — derived from
///     <c>LegalBreachCheckIds</c>; informational only, does NOT change gating.</item>
/// </list>
/// The pipeline gate ALWAYS remains on <see cref="RuleFinding.Verdict"/>; do not change that.
/// </para>
/// </remarks>
public sealed class VerdictAggregator : IVerdictAggregator
{
    /// <inheritdoc />
    public Result<VerdictSummary> Aggregate(
        IReadOnlyList<RuleFinding> findings,
        BlockedOutcome? blocked = null,
        CancellationToken ct = default,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<VerdictSummary>();

        if (findings is null)
            return Result<VerdictSummary>.WithFailure("findings must not be null.");

        // ------------------------------------------------------------------
        // Precedence 1: BLOCKED (binding never completed)
        // ------------------------------------------------------------------
        if (blocked is not null)
            return Result<VerdictSummary>.WithSuccess(
                VerdictSummary.Blocked(blocked, tenantDeviations));

        // ------------------------------------------------------------------
        // Partition findings by verdict category and legal-signal separation
        // ------------------------------------------------------------------
        List<string> failIds = [];
        List<string> insufficientIds = [];
        List<string> legalBreachIds = [];
        List<string> tenantOnlyFailIds = [];
        int passCount = 0;

        foreach (var finding in findings)
        {
            switch (finding.Verdict)
            {
                case FindingVerdict.Fail:
                    failIds.Add(finding.CheckId);

                    // Legal-baseline separation: is this a regulatory breach or tenant-only?
                    if (finding.LegalBaselineVerdict == FindingVerdict.Fail)
                        legalBreachIds.Add(finding.CheckId);
                    else
                        tenantOnlyFailIds.Add(finding.CheckId);

                    break;

                case FindingVerdict.InsufficientData:
                    insufficientIds.Add(finding.CheckId);
                    break;

                case FindingVerdict.Pass:
                default:
                    passCount++;
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Precedence 2: RED — at least one Fail finding
        // ------------------------------------------------------------------
        if (failIds.Count > 0)
        {
            return Result<VerdictSummary>.WithSuccess(
                VerdictSummary.Red(
                    failCount: failIds.Count,
                    passCount: passCount,
                    insufficientDataCount: insufficientIds.Count,
                    failCheckIds: failIds,
                    insufficientDataCheckIds: insufficientIds,
                    tenantDeviations: tenantDeviations,
                    legalBreachCheckIds: legalBreachIds,
                    tenantOnlyFailCheckIds: tenantOnlyFailIds));
        }

        // ------------------------------------------------------------------
        // Precedence 3: GREEN — all Pass / InsufficientData (or empty)
        // ------------------------------------------------------------------
        return Result<VerdictSummary>.WithSuccess(
            VerdictSummary.Green(
                passCount: passCount,
                insufficientDataCount: insufficientIds.Count,
                insufficientDataCheckIds: insufficientIds,
                tenantDeviations: tenantDeviations,
                legalBreachCheckIds: legalBreachIds,
                tenantOnlyFailCheckIds: tenantOnlyFailIds));
    }
}
