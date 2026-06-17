using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
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
/// </remarks>
public sealed class VerdictAggregator : IVerdictAggregator
{
    /// <inheritdoc />
    public Result<VerdictSummary> Aggregate(
        IReadOnlyList<RuleFinding> findings,
        BlockedOutcome? blocked = null,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<VerdictSummary>();

        if (findings is null)
            return Result<VerdictSummary>.WithFailure("findings must not be null.");

        // ------------------------------------------------------------------
        // Precedence 1: BLOCKED (binding never completed)
        // ------------------------------------------------------------------
        if (blocked is not null)
            return Result<VerdictSummary>.WithSuccess(VerdictSummary.Blocked(blocked));

        // ------------------------------------------------------------------
        // Partition findings by verdict category
        // ------------------------------------------------------------------
        List<string> failIds = [];
        List<string> insufficientIds = [];
        int passCount = 0;

        foreach (var finding in findings)
        {
            switch (finding.Verdict)
            {
                case FindingVerdict.Fail:
                    failIds.Add(finding.CheckId);
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
                    insufficientDataCheckIds: insufficientIds));
        }

        // ------------------------------------------------------------------
        // Precedence 3: GREEN — all Pass / InsufficientData (or empty)
        // ------------------------------------------------------------------
        return Result<VerdictSummary>.WithSuccess(
            VerdictSummary.Green(
                passCount: passCount,
                insufficientDataCount: insufficientIds.Count,
                insufficientDataCheckIds: insufficientIds));
    }
}
