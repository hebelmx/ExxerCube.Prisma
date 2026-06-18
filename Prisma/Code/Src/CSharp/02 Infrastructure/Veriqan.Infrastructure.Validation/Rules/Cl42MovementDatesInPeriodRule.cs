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
/// CL-42: Validates that every movement's OperationDate falls within the billing period
/// [PeriodStart .. PeriodCutDate] (inclusive on both ends).
/// </summary>
/// <remarks>
/// <para>
/// Movements with a <see langword="null"/> OperationDate are skipped — the extractor
/// was unable to parse the date, so CL-42 cannot evaluate those rows.
/// CL-42 only fails on rows where a parsed date is provably outside the period.
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
            .Where(m => m.OperationDate.HasValue
                        && (m.OperationDate.Value < start || m.OperationDate.Value > cut))
            .ToList();

        if (outOfRange.Count > 0)
        {
            var first = outOfRange[0];
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: $"{start:yyyy-MM-dd}–{cut:yyyy-MM-dd}",
                    observed: $"{first.OperationDate!.Value:yyyy-MM-dd} ({outOfRange.Count} out-of-range date(s))",
                    toleranceApplied: null,
                    locator: first.Locator));
        }

        var datedCount = model.Movements.Count(m => m.OperationDate.HasValue);

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Pass(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                observed: $"All {datedCount} movement date(s) within {start:yyyy-MM-dd}–{cut:yyyy-MM-dd}",
                toleranceApplied: null));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
