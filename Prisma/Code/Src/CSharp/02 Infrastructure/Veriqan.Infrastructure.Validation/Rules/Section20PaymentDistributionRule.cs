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
/// LAW-§20-WATERFALL: Validates the §20 "Distribución de tu último pago" 7-column
/// payment-distribution waterfall identity mandated by the CONDUSEF Acuerdo §20.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula (Acuerdo §20):</b>
/// <c>|Pagos y abonos| ≈ Regulares + AMesesSinIntereses + AMesesConIntereses + InteresesYComisiones + IVA − SaldoAFavor</c>
/// </para>
/// <para>Column semantic order (§20 single row, 7 value cells):</para>
/// <list type="bullet">
///   <item>[0] Pagos y abonos — reported as a negative signed amount; compare absolute value.</item>
///   <item>[1] Compras y cargos Regulares.</item>
///   <item>[2] A meses SIN intereses (capital portion).</item>
///   <item>[3] A meses CON intereses (capital portion).</item>
///   <item>[4] Intereses y comisiones.</item>
///   <item>[5] IVA sobre intereses y comisiones.</item>
///   <item>[6] Saldo a favor (subtracted — credit-balance rebate).</item>
/// </list>
/// <para>
/// <b>Preventive gate semantics:</b> a false Fail would halt a bank's billing run.
/// The rule MUST return <see cref="FindingVerdict.InsufficientData"/> on any missing or
/// low-confidence cell — never guess, never false-Fail.
/// </para>
/// <para>
/// <b>Tolerance (ADR-V3):</b> resolved via <see cref="ILegalToleranceProvider"/>.
/// Legal default 0.50 MXN. Dual verdict: <see cref="RuleFinding.LegalBaselineVerdict"/>
/// reflects the legal floor; <see cref="RuleFinding.Verdict"/> reflects the tenant-effective bar.
/// </para>
/// </remarks>
internal sealed class Section20PaymentDistributionRule : IVecValidationRule
{
    private const string Version = "1.0.0";
    private const int Section20Number = 20;
    private const int RequiredValueCellCount = 7;

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Section20PaymentDistributionRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Section20PaymentDistributionRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "LAW-§20-WATERFALL";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §20";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.TenantTightenableOnly;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Resolve LEGAL tolerance: always use LegalDefault (no bundle override — Story 9.6)
        var legalTolerance = _toleranceProvider.For(CheckId).Resolve(null).EffectiveValue;

        // Resolve TENANT-EFFECTIVE tolerance
        var effectiveTolerance = ctx.TenantProfile?.GetEffectiveTolerance(CheckId, legalTolerance)
            ?? legalTolerance;

        // Guard: StatementModel must be present
        if (ctx.StatementModel is null)
            return InsufficientData("StatementModel is not populated.");

        // Guard: §20 table must be present and fully extracted
        var table20 = ctx.StatementModel.FinancialTables
            .FirstOrDefault(t => t.SectionNumber == Section20Number);

        if (table20 is null || table20.Status == TableExtractionStatus.SectionNotFound)
            return InsufficientData("§20 table is absent (SectionNotFound).");

        if (table20.Status != TableExtractionStatus.Extracted)
            return InsufficientData($"§20 table extraction status is {table20.Status} — not Extracted.");

        // Guard: §20 must have exactly one row with 7 value cells
        if (table20.Rows.Count == 0)
            return InsufficientData("§20 table has no rows.");

        var row = table20.Rows[0];
        if (row.Values.Count < RequiredValueCellCount)
            return InsufficientData(
                $"§20 row has {row.Values.Count} value cells; expected at least {RequiredValueCellCount}.");

        // Confidence threshold
        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        // Validate each of the 7 cells — must be Amount kind, have a parsed value, and meet confidence
        for (var i = 0; i < RequiredValueCellCount; i++)
        {
            var cell = row.Values[i];

            if (cell.Kind != CellKind.Amount)
                return InsufficientData(
                    $"§20 column [{i}] has kind {cell.Kind}; expected Amount.");

            if (cell.ParsedValue is null)
                return InsufficientData(
                    $"§20 column [{i}] has no parsed value (raw: \"{cell.RawText}\").");

            if (cell.Confidence < confidenceThreshold)
                return InsufficientData(
                    $"§20 column [{i}] confidence {cell.Confidence:F2} < required {confidenceThreshold:F2}.");
        }

        // All 7 cells are valid — compute the identity
        //
        // col[0] = Pagos y abonos (typically a negative signed amount; take absolute value)
        // col[1] = Compras y cargos Regulares
        // col[2] = A meses SIN intereses
        // col[3] = A meses CON intereses
        // col[4] = Intereses y comisiones
        // col[5] = IVA
        // col[6] = Saldo a favor (subtracted — credit-balance rebate)
        var pagosYAbonos = Math.Abs(row.Values[0].ParsedValue!.Value);
        var components = row.Values[1].ParsedValue!.Value   // Regulares
                       + row.Values[2].ParsedValue!.Value   // AMesesSinIntereses
                       + row.Values[3].ParsedValue!.Value   // AMesesConIntereses
                       + row.Values[4].ParsedValue!.Value   // InteresesYComisiones
                       + row.Values[5].ParsedValue!.Value   // IVA
                       - row.Values[6].ParsedValue!.Value;  // SaldoAFavor (subtracted)

        var diff = Math.Abs(pagosYAbonos - components);
        var locator = table20.Locator;

        var legalPasses = diff <= legalTolerance;
        var tenantPasses = diff <= effectiveTolerance;

        if (tenantPasses)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"{pagosYAbonos:F2}",
                    toleranceApplied: effectiveTolerance,
                    locator: locator,
                    legalBaselineVerdict: legalPasses ? FindingVerdict.Pass : FindingVerdict.Fail));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"{components:F2}",
                observed: $"{pagosYAbonos:F2}",
                toleranceApplied: effectiveTolerance,
                locator: locator,
                legalBaselineVerdict: legalPasses ? FindingVerdict.Pass : FindingVerdict.Fail));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
