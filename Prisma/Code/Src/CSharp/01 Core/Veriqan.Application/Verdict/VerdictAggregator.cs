using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
/// <para>
/// <b>Unrecognised-verdict safety (S12):</b>
/// If a future enum addition produces a <see cref="FindingVerdict"/> value not handled by
/// the switch, the finding is treated as <see cref="FindingVerdict.InsufficientData"/>
/// (abstain) rather than silently counting as a pass. This prevents a latent false-GREEN
/// risk when new enum values are added without a corresponding aggregator update.
/// A <see cref="LogLevel.Warning"/> is emitted so the gap is observable at runtime.
/// </para>
/// </remarks>
public sealed class VerdictAggregator : IVerdictAggregator
{
    private readonly ILogger<VerdictAggregator> _logger;

    /// <summary>
    /// Initialises a new <see cref="VerdictAggregator"/>.
    /// </summary>
    /// <param name="logger">
    /// Optional structured logger. When <see langword="null"/> (e.g. in unit tests that use
    /// <c>new VerdictAggregator()</c> directly), a <see cref="NullLogger{T}"/> is substituted
    /// so the parameterless construction path remains valid.
    /// </param>
    public VerdictAggregator(ILogger<VerdictAggregator>? logger = null)
    {
        _logger = logger ?? NullLogger<VerdictAggregator>.Instance;
    }

    /// <inheritdoc />
    public Result<VerdictSummary> Aggregate(
        IReadOnlyList<RuleFinding> findings,
        BlockedOutcome? blocked = null,
        CancellationToken ct = default,
        IReadOnlyList<TenantDeviation>? tenantDeviations = null,
        IReadOnlyDictionary<string, ChecklistTier>? checklistTiers = null)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<VerdictSummary>();

        if (findings is null)
            return Result<VerdictSummary>.WithFailure("findings must not be null.");

        // ------------------------------------------------------------------
        // Precedence 1: non-verdict (binding never completed)
        // Story 4.2: route to ExtractionGap or Blocked depending on reason.
        // All four currently-wired BlockReason values map to ExtractionGap.
        // The Blocked factory is reserved for future document-defect reasons
        // (EncryptedDocument, CorruptDocument, TamperedDocument, etc.) once wired.
        // ------------------------------------------------------------------
        if (blocked is not null)
            return Result<VerdictSummary>.WithSuccess(
                IsExtractionGapReason(blocked.Reason)
                    ? VerdictSummary.ExtractionGap(blocked, tenantDeviations)
                    : VerdictSummary.Blocked(blocked, tenantDeviations));

        // ------------------------------------------------------------------
        // Partition findings by verdict category and legal-signal separation
        // ------------------------------------------------------------------
        List<string> failIds = [];
        List<string> insufficientIds = [];
        List<string> legalBreachIds = [];
        List<string> tenantOnlyFailIds = [];
        int passCount = 0;

        // Story 4.1: track the minimum confidence across all findings for the verdict rollup.
        double minConfidence = 1.0;
        bool hasAnyFinding = false;

        foreach (var finding in findings)
        {
            // Story 4.1: accumulate minimum confidence across all findings.
            if (!hasAnyFinding || finding.Confidence < minConfidence)
            {
                minConfidence = finding.Confidence;
                hasAnyFinding = true;
            }

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
                    passCount++;
                    break;

                default:
                    // Unrecognised verdict — abstain rather than silently count as pass
                    // (false-GREEN prevention, S12). Treat identically to InsufficientData
                    // so the finding never escalates to RED either.
                    _logger.LogWarning(
                        "VerdictAggregator encountered unrecognised {FindingVerdict} on check {CheckId}; treating as InsufficientData to prevent false-GREEN.",
                        (int)finding.Verdict,
                        finding.CheckId);
                    insufficientIds.Add(finding.CheckId);
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Precedence 2: RED (or YELLOW with tier map) — at least one Fail
        // ------------------------------------------------------------------
        if (failIds.Count > 0)
        {
            // Story 1.2: when a tier map is supplied, partition the fails and compute
            // per-tier verdicts.  With a null map the legacy path is preserved exactly.
            if (checklistTiers is not null)
            {
                List<string> bankFailIds = [];
                List<string> condusefFailIds = [];

                foreach (var checkId in failIds)
                {
                    // Missing key → Condusef (conservative: never silently drop from RED).
                    ChecklistTier tier = checklistTiers.TryGetValue(checkId, out var t)
                        ? t
                        : ChecklistTier.Condusef;

                    if (tier is ChecklistTier.Bank or ChecklistTier.Both)
                        bankFailIds.Add(checkId);

                    if (tier is ChecklistTier.Condusef or ChecklistTier.Both)
                        condusefFailIds.Add(checkId);
                }

                VerdictSignal bankTier = bankFailIds.Count > 0
                    ? VerdictSignal.Yellow
                    : VerdictSignal.Green;

                VerdictSignal condusefTier = condusefFailIds.Count > 0
                    ? VerdictSignal.Red
                    : VerdictSignal.Green;

                VerdictSignal overall = VerdictSummary.CombineOverallSignal(bankTier, condusefTier);

                return Result<VerdictSummary>.WithSuccess(
                    VerdictSummary.Red(
                        failCount: failIds.Count,
                        passCount: passCount,
                        insufficientDataCount: insufficientIds.Count,
                        failCheckIds: failIds,
                        insufficientDataCheckIds: insufficientIds,
                        tenantDeviations: tenantDeviations,
                        legalBreachCheckIds: legalBreachIds,
                        tenantOnlyFailCheckIds: tenantOnlyFailIds,
                        bankFailCheckIds: bankFailIds,
                        condusefFailCheckIds: condusefFailIds,
                        bankTierVerdict: bankTier,
                        condusefTierVerdict: condusefTier,
                        signal: overall,
                        confidence: minConfidence));
            }

            // Legacy (null-map) path — behavior unchanged.
            return Result<VerdictSummary>.WithSuccess(
                VerdictSummary.Red(
                    failCount: failIds.Count,
                    passCount: passCount,
                    insufficientDataCount: insufficientIds.Count,
                    failCheckIds: failIds,
                    insufficientDataCheckIds: insufficientIds,
                    tenantDeviations: tenantDeviations,
                    legalBreachCheckIds: legalBreachIds,
                    tenantOnlyFailCheckIds: tenantOnlyFailIds,
                    confidence: minConfidence));
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
                tenantOnlyFailCheckIds: tenantOnlyFailIds,
                confidence: minConfidence));
    }

    // -----------------------------------------------------------------------
    // Story 4.2: reason-to-signal routing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="reason"/> maps to
    /// <see cref="VerdictSignal.ExtractionGap"/> (permanent system/engineering capability gap).
    /// Returns <see langword="false"/> for future document-defect reasons that will route to
    /// <see cref="VerdictSignal.Blocked"/> once detection is wired.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Routing decision rule (Story 4.2): "Could the same document bytes, resubmitted tomorrow
    /// with NO human action, produce a verdict?"
    /// <list type="bullet">
    ///   <item>ExtractionGap reasons — No, but engineering could fix the system.</item>
    ///   <item>Blocked reasons (future) — No, and only human action on the document would help.</item>
    /// </list>
    /// </para>
    /// <para>
    /// All four currently-wired <see cref="BlockReason"/> values are ExtractionGap reasons.
    /// Future taxonomy members that represent capability/config gaps
    /// (<see cref="BlockReason.AmbiguousDocumentScope"/>,
    /// <see cref="BlockReason.MissingMandatoryAnchorFields"/>,
    /// <see cref="BlockReason.RefDataVersionMismatch"/>) are also included here even though
    /// their detection is not yet wired.
    /// </para>
    /// </remarks>
    private static bool IsExtractionGapReason(BlockReason reason) =>
        reason is BlockReason.UnknownProduct
               or BlockReason.InvalidBundle
               or BlockReason.InsufficientExtractionCoverage
               or BlockReason.InsufficientTextLayer
               or BlockReason.AmbiguousDocumentScope
               or BlockReason.MissingMandatoryAnchorFields
               or BlockReason.RefDataVersionMismatch;
}
