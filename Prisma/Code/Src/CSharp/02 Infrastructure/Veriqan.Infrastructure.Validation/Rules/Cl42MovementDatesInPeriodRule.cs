using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-42: Validates that every movement's ChargeDate falls within the billing period
/// [PeriodStart .. PeriodCutDate] (inclusive on both ends).
/// </summary>
/// <remarks>
/// <para>
/// <b>RC1.S4.b residual fix (chunk B4):</b> the period-membership invariant is
/// <c>ChargeDate ∈ [PeriodStart, CutDate]</c> — "Fecha de cargo" (when the amount actually
/// posted), not "Fecha de la operación" (when the transaction occurred). Real bank behavior:
/// a weekend purchase (e.g. a Saturday, previous period) legitimately POSTS on the first day
/// of the current period. Gating on OperationDate produced a false Critical on a printed-correct
/// statement (real-corpus triage evidence: <c>docs/qa/calibration/real-corpus-triage-2026-07.md</c>).
/// </para>
/// <para>
/// <b>Supplementary sanity bound:</b> a movement whose OperationDate falls AFTER the period's
/// CutDate is still flagged even if its ChargeDate is missing or in-range — a transaction cannot
/// be dated in the future relative to the period it is billed in. OperationDate preceding
/// PeriodStart is deliberately NOT flagged (see above).
/// </para>
/// <para>
/// Movements with a <see langword="null"/> ChargeDate are skipped by the primary invariant —
/// the extractor was unable to parse the date (including the PDF year-truncation case), so
/// CL-42 cannot evaluate those rows on that column. CL-42 only fails on rows where a parsed
/// date is provably outside the period.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item>StatementModel or PeriodSummary is null.</item>
///   <item>PeriodStart or PeriodCutDate is not <see cref="ExtractionStatus.Extracted"/>.</item>
///   <item>No movements extracted (empty + status ≠ Extracted).</item>
/// </list>
/// </para>
/// <para>ToleranceApplied is null (date comparison has no numeric tolerance).</para>
/// </remarks>
internal sealed class Cl42MovementDatesInPeriodRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-42";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §22";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var model = ctx.StatementModel;
        if (model is null)
            return InsufficientData("StatementModel is not populated.");

        var ps = model.PeriodSummary;
        if (ps is null)
            return InsufficientData("PeriodSummary is not populated.");

        if (ps.PeriodStart.Status != ExtractionStatus.Extracted)
            return InsufficientData($"PeriodStart is {ps.PeriodStart.Status}.");

        if (ps.PeriodCutDate.Status != ExtractionStatus.Extracted)
            return InsufficientData($"PeriodCutDate is {ps.PeriodCutDate.Status}.");

        if (model.MovementsStatus != MovementsExtractionStatus.Extracted || model.Movements.Count == 0)
            return InsufficientData($"DESGLOSE movements not extracted (status: {model.MovementsStatus}).");

        var start = ps.PeriodStart.Value;
        var cut = ps.PeriodCutDate.Value;

        var outOfRange = model.Movements
            .Where(m => IsOutOfPeriod(m, start, cut))
            .ToList();

        if (outOfRange.Count > 0)
        {
            var first = outOfRange[0];
            var offendingDate = ChargeDateOutOfRange(first, start, cut)
                ? first.ChargeDate!.Value
                : first.OperationDate!.Value;
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: $"{start:yyyy-MM-dd}–{cut:yyyy-MM-dd}",
                    observed: $"{offendingDate:yyyy-MM-dd} ({outOfRange.Count} out-of-range date(s))",
                    toleranceApplied: null,
                    locator: first.Locator));
        }

        var datedCount = model.Movements.Count(m => m.ChargeDate.HasValue || m.OperationDate.HasValue);

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Pass(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                observed: $"All {datedCount} movement date(s) within {start:yyyy-MM-dd}–{cut:yyyy-MM-dd}",
                toleranceApplied: null));
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="m"/> violates the CL-42 period
    /// invariant: primary check on <see cref="StatementMovement.ChargeDate"/> (null-skip),
    /// supplementary check that <see cref="StatementMovement.OperationDate"/> is not after
    /// <paramref name="cut"/> (RC1.S4.b — a future-dated operation is still suspicious even
    /// when ChargeDate is missing or in-range; an operation preceding <paramref name="start"/>
    /// is NOT flagged, since real bank behavior lets it legitimately post in-period).
    /// </summary>
    private static bool IsOutOfPeriod(StatementMovement m, DateOnly start, DateOnly cut) =>
        ChargeDateOutOfRange(m, start, cut)
        || (m.OperationDate.HasValue && m.OperationDate.Value > cut);

    private static bool ChargeDateOutOfRange(StatementMovement m, DateOnly start, DateOnly cut) =>
        m.ChargeDate.HasValue && (m.ChargeDate.Value < start || m.ChargeDate.Value > cut);

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
