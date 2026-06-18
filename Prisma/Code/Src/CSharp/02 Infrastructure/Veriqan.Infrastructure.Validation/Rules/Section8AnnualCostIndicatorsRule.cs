using System;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// LAW-§8-INDICADORES: Validates that the three mandatory §8 "Indicadores del costo
/// anual de la tarjeta" 12-month cost indicators are present and non-negative.
/// </summary>
/// <remarks>
/// <para>
/// <b>The three mandated indicators (Acuerdo §8, row order):</b>
/// </para>
/// <list type="bullet">
///   <item>[0] Monto de intereses pagados en los últimos 12 meses.</item>
///   <item>[1] Monto de comisiones totales pagadas en los últimos 12 meses.</item>
///   <item>[2] Monto de anualidad o comisiones por administración pagadas en los últimos 12 meses.</item>
/// </list>
/// <para>
/// Each indicator must be present (value cell not Empty/Missing) and non-negative.
/// The indicators represent 12-month cumulative costs; negative values are legally invalid.
/// </para>
/// <para>
/// <b>Preventive gate semantics:</b> a false Fail halts a bank's billing run.
/// </para>
/// <list type="bullet">
///   <item>
///     <see cref="TableExtractionStatus.SectionNotFound"/> → <see cref="FindingVerdict.Fail"/>
///     (the section heading was definitively not found in an otherwise well-parsed document —
///     the law mandates §8 on every VEC statement).
///   </item>
///   <item>
///     <see cref="TableExtractionStatus.Indeterminate"/> or
///     <see cref="TableExtractionStatus.NoRowsParsed"/> → <see cref="FindingVerdict.InsufficientData"/>
///     (extraction was uncertain; abstain rather than risk a false Fail).
///   </item>
///   <item>
///     <see cref="TableExtractionStatus.Extracted"/> but an indicator row's value cell is
///     <see cref="CellKind.Empty"/> → <see cref="FindingVerdict.Fail"/> (the bank published the
///     section but omitted a legally-mandated indicator).
///   </item>
///   <item>
///     <see cref="TableExtractionStatus.Extracted"/> but a value cell has low confidence or
///     no parsed value → <see cref="FindingVerdict.InsufficientData"/> (uncertain read; abstain).
///   </item>
///   <item>
///     All three indicators present, parseable, and non-negative → <see cref="FindingVerdict.Pass"/>.
///   </item>
///   <item>
///     Any indicator value is negative → <see cref="FindingVerdict.Fail"/> (indicators must be ≥ 0).
///   </item>
/// </list>
/// <para>
/// <b>Coherence (best-effort only):</b> this rule does NOT apply a coherence/recompute check.
/// The indicators are 12-month rolling figures that cannot be independently derived from the
/// single-period statement values; any coherence formula would introduce false-Fail risk.
/// The tolerance registered under <c>LAW-§8-INDICADORES</c> is present for future use only.
/// </para>
/// </remarks>
internal sealed class Section8AnnualCostIndicatorsRule : IVecValidationRule
{
    private const string Version = "1.0.0";
    private const int Section8Number = 8;
    private const int RequiredRowCount = 3;

    // Human-readable labels for the three indicators (for Fail messages).
    private static readonly string[] IndicatorLabels =
    [
        "Monto de intereses pagados en los últimos 12 meses",
        "Monto de comisiones totales pagadas en los últimos 12 meses",
        "Monto de anualidad o comisiones por administración pagadas en los últimos 12 meses",
    ];

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Section8AnnualCostIndicatorsRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Section8AnnualCostIndicatorsRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "LAW-§8-INDICADORES";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §8";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.TenantTightenableOnly;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Tolerance is registered (for best-effort coherence future use); resolve it so
        // the provider throws early on misconfiguration rather than silently missing.
        var legalTolerance = _toleranceProvider.For(CheckId).Resolve(null).EffectiveValue;

        // Guard: StatementModel must be present.
        if (ctx.StatementModel is null)
            return InsufficientData("StatementModel is not populated.");

        // Find the §8 table.
        var table8 = ctx.StatementModel.FinancialTables
            .FirstOrDefault(t => t.SectionNumber == Section8Number);

        // §8 entirely absent from the FinancialTables collection — uncertain state.
        if (table8 is null)
            return InsufficientData("§8 table entry is absent from FinancialTables.");

        var locator = table8.Locator;

        // Dispatch on extraction status.
        //
        // SectionNotFound: the extractor searched the entire document and confirmed §8 is
        // absent. This is a definitive compliance gap — Fail citing §8.
        //
        // Indeterminate / NoRowsParsed: the extractor found §8's heading but could not
        // reliably reconstruct the rows. We cannot tell whether the indicators are truly
        // absent or just unreadable — abstain to prevent a false Fail.
        if (table8.Status == TableExtractionStatus.SectionNotFound)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: "§8 Indicadores del costo anual de la tarjeta present with 3 indicators",
                    observed: "§8 section not found in document",
                    toleranceApplied: legalTolerance,
                    locator: locator,
                    legalBaselineVerdict: FindingVerdict.Fail));
        }

        if (table8.Status != TableExtractionStatus.Extracted)
        {
            return InsufficientData(
                $"§8 table extraction status is {table8.Status} — cannot reliably verify indicators.");
        }

        // §8 is Extracted. Validate that all 3 indicator rows are present and usable.
        if (table8.Rows.Count < RequiredRowCount)
        {
            // Fewer rows than expected — extraction partial; abstain rather than Fail
            // (the extractor may have simply missed a row due to layout difficulties).
            return InsufficientData(
                $"§8 table has {table8.Rows.Count} row(s); expected {RequiredRowCount}. " +
                "Cannot reliably determine whether all indicators are present.");
        }

        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        for (var i = 0; i < RequiredRowCount; i++)
        {
            var row = table8.Rows[i];

            // §8 rows have exactly 1 value cell (the indicator amount).
            if (row.Values.Count == 0)
            {
                // Row exists but has no value cell at all — treat as a missing indicator → Fail.
                return Result<RuleFinding>.WithSuccess(
                    RuleFinding.Fail(
                        checkId: CheckId,
                        technique: Technique,
                        severity: FindingSeverity.Critical,
                        engineVersion: Version,
                        expected: $"§8 indicator [{i}] ({IndicatorLabels[i]}) present with a value",
                        observed: $"§8 indicator [{i}] row has no value cells",
                        toleranceApplied: legalTolerance,
                        locator: locator,
                        legalBaselineVerdict: FindingVerdict.Fail));
            }

            var cell = row.Values[0];

            // Cell kind Empty or Missing (CellKind.Empty covers both the EmptyCell and Missing
            // factory paths — both return Kind=Empty). ParsedValue is null in either case.
            // This means the bank published §8 but left the indicator blank — Fail.
            if (cell.Kind == CellKind.Empty)
            {
                return Result<RuleFinding>.WithSuccess(
                    RuleFinding.Fail(
                        checkId: CheckId,
                        technique: Technique,
                        severity: FindingSeverity.Critical,
                        engineVersion: Version,
                        expected: $"§8 indicator [{i}] ({IndicatorLabels[i]}) has a value",
                        observed: $"§8 indicator [{i}] value cell is empty/missing (raw: \"{cell.RawText}\")",
                        toleranceApplied: legalTolerance,
                        locator: cell.Locator,
                        legalBaselineVerdict: FindingVerdict.Fail));
            }

            // A non-empty cell with no parsed value or below the confidence threshold —
            // the OCR could not read the number reliably. Abstain.
            if (cell.ParsedValue is null)
            {
                return InsufficientData(
                    $"§8 indicator [{i}] ({IndicatorLabels[i]}) value cell has no parsed value " +
                    $"(raw: \"{cell.RawText}\", kind: {cell.Kind}).");
            }

            if (cell.Confidence < confidenceThreshold)
            {
                return InsufficientData(
                    $"§8 indicator [{i}] ({IndicatorLabels[i]}) confidence {cell.Confidence:F2} " +
                    $"< required {confidenceThreshold:F2}.");
            }

            // Indicator values must be non-negative (12-month cumulative costs cannot be negative).
            if (cell.ParsedValue.Value < 0m)
            {
                return Result<RuleFinding>.WithSuccess(
                    RuleFinding.Fail(
                        checkId: CheckId,
                        technique: Technique,
                        severity: FindingSeverity.Critical,
                        engineVersion: Version,
                        expected: $"§8 indicator [{i}] ({IndicatorLabels[i]}) ≥ 0",
                        observed: $"§8 indicator [{i}] value is {cell.ParsedValue.Value:F2} (negative)",
                        toleranceApplied: legalTolerance,
                        locator: cell.Locator,
                        legalBaselineVerdict: FindingVerdict.Fail));
            }
        }

        // All three indicators present, parseable, non-negative.
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Pass(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                observed: $"3 §8 indicators present and non-negative",
                toleranceApplied: legalTolerance,
                locator: locator,
                legalBaselineVerdict: FindingVerdict.Pass));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
