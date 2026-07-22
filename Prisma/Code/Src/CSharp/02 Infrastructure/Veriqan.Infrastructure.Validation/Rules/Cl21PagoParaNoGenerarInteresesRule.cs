using System;
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
/// CL-21: Validates the printed "Pago para no generar intereses" against the formula:
/// <c>PagoParaNoGenerarIntereses = AdeudoPeriodoAnterior + CargosRegularesNoMeses
///     + CargosComprasAMesesCapital + MontoIntereses + MontoComisiones
///     + IvaInteresesYComisiones − PagosYAbonos</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tolerance (ADR-V3, Story 9.6):</b> resolved via <see cref="ILegalToleranceProvider"/>
/// using <c>Resolve(null)</c> for the legal floor; tenant overrides come from
/// <c>ResolvedTenantProfile.GetEffectiveTolerance</c>.
/// </para>
/// <para>
/// <b>Dual verdict (Story 9.6):</b> <see cref="RuleFinding.LegalBaselineVerdict"/> reflects the
/// legal floor; <see cref="RuleFinding.Verdict"/> reflects the tenant-effective bar.
/// </para>
/// <para>
/// <b>Confidence guard (Story 9.5 / 9.6):</b> each RESUMEN subtotal input field is checked
/// for extraction confidence. If a PRESENT (Extracted) field is below the threshold the rule
/// abstains (InsufficientData) to prevent a false verdict from a misread digit.
/// </para>
/// <para>
/// <b>Guarded implied-zero (Epic 5 / F1):</b> <see cref="PeriodSummary.AdeudoPeriodoAnterior"/>
/// and <see cref="PeriodSummary.PagosYAbonos"/> may be legitimately absent because Banamex
/// suppresses zero-value RESUMEN rows in certain PDF layouts.
/// <see cref="ExtractionStatus.ExtractedInvalidFormat"/> (label IS typeset but the amount is
/// unparseable — e.g. OCR-corrupted digit) causes abstention (InsufficientData) rather than a
/// silent implied-zero: substituting 0m for a printed-but-unreadable amount is dishonest.
/// A PRESENT (<see cref="ExtractionStatus.Extracted"/>) but low-confidence field also causes
/// abstention because a misread digit in a non-zero row could corrupt the formula silently.
/// </para>
/// <para>
/// <b>RC1.S4.a — grounded implied-zero (real-corpus triage, class c):</b> real Banamex credit-
/// card statements print <c>AdeudoPeriodoAnterior</c>/<c>PagosYAbonos</c> as IMAGES when they are
/// genuinely non-zero (the text layer shows no label/amount at all — the same
/// <see cref="ExtractionStatus.NotExtracted"/> signature Epic 5 assumed meant "row genuinely
/// omitted because it is zero"). Evidence: two real months showed implied-zero deltas of
/// +$409.13 / +$2,159.45 (<c>docs/qa/calibration/real-corpus-triage-2026-07.md</c>) — "label
/// never typeset ⇒ zero" is falsified as a universal rule. <see cref="ExtractionStatus.NotExtracted"/>
/// therefore now only implies zero when it is <b>grounded</b>: the operand's RESUMEN label text
/// (<see cref="ZeroSuppressionLabels"/>) must itself be found somewhere in
/// <see cref="StatementModel.NormalizedFullText"/> (the "label IS present in the text layer with
/// a zero/suppressed amount" evidence pattern). When the operand is NotExtracted and its label is
/// not found anywhere in the text layer, the rule abstains (InsufficientData) instead of silently
/// computing on a fabricated zero — "misread digit ≠ false non-compliant" (never force a pass).
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item>Any of the five CORE operands (CargosRegularesNoMeses, CargosComprasAMesesCapital,
///     MontoIntereses, MontoComisiones, IvaInteresesYComisiones) is
///     <see cref="ExtractionStatus.NotExtracted"/> — signals a genuine RESUMEN parse failure.</item>
///   <item><see cref="PeriodSummary.PagoParaNoGenerarIntereses"/> is NotExtracted — the payment
///     block was not located, so there is nothing to compare against.</item>
///   <item>AdeudoPeriodoAnterior or PagosYAbonos is <see cref="ExtractionStatus.ExtractedInvalidFormat"/>
///     — the label was found but the amount could not be parsed; implying zero would be dishonest.</item>
///   <item>AdeudoPeriodoAnterior or PagosYAbonos is <b>present</b> (Extracted) but below the
///     confidence threshold — a low-confidence value in a zero-row that is actually non-zero
///     could silently corrupt the formula.</item>
///   <item>AdeudoPeriodoAnterior or PagosYAbonos is <see cref="ExtractionStatus.NotExtracted"/> AND
///     its RESUMEN label is not found anywhere in <see cref="StatementModel.NormalizedFullText"/>
///     — the implied-zero grounding evidence is absent (RC1.S4.a); the row may be a genuinely
///     non-zero, image-rendered amount rather than a zero-suppressed row.</item>
///   <item>Any other confidence-bearing field is below the confidence threshold.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl21PagoParaNoGenerarInteresesRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <summary>
    /// RESUMEN label text (normalized: uppercase, accent-stripped) for
    /// <see cref="PeriodSummary.AdeudoPeriodoAnterior"/> and <see cref="PeriodSummary.PagosYAbonos"/>,
    /// used as the RC1.S4.a grounding evidence for the implied-zero branch. A
    /// <see cref="ExtractionStatus.NotExtracted"/> field only implies zero when its label is found
    /// somewhere in <see cref="StatementModel.NormalizedFullText"/>.
    /// </summary>
    private static class ZeroSuppressionLabels
    {
        public const string AdeudoPeriodoAnterior = "ADEUDO DEL PERIODO ANTERIOR";
        public const string PagosYAbonos = "PAGOS Y ABONOS";
    }

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Cl21PagoParaNoGenerarInteresesRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Cl21PagoParaNoGenerarInteresesRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "CL-21";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §7";

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

        var ps = ctx.StatementModel?.PeriodSummary;
        if (ps is null)
            return InsufficientData("StatementModel or PeriodSummary is not populated.");

        // Five CORE operands must be Extracted — any absence signals that the RESUMEN block
        // genuinely failed to parse (not a zero-row suppression scenario).
        // AdeudoPeriodoAnterior and PagosYAbonos are intentionally excluded from this hard guard;
        // see the guarded implied-zero blocks below.
        if (ps.CargosRegularesNoMeses.Status != ExtractionStatus.Extracted)
            return InsufficientData($"CargosRegularesNoMeses is {ps.CargosRegularesNoMeses.Status}.");
        if (ps.CargosComprasAMesesCapital.Status != ExtractionStatus.Extracted)
            return InsufficientData($"CargosComprasAMesesCapital is {ps.CargosComprasAMesesCapital.Status}.");
        if (ps.MontoIntereses.Status != ExtractionStatus.Extracted)
            return InsufficientData($"MontoIntereses is {ps.MontoIntereses.Status}.");
        if (ps.MontoComisiones.Status != ExtractionStatus.Extracted)
            return InsufficientData($"MontoComisiones is {ps.MontoComisiones.Status}.");
        if (ps.IvaInteresesYComisiones.Status != ExtractionStatus.Extracted)
            return InsufficientData($"IvaInteresesYComisiones is {ps.IvaInteresesYComisiones.Status}.");

        // Target field — its presence anchors the payment block; absence means the block was not located.
        if (ps.PagoParaNoGenerarIntereses.Status != ExtractionStatus.Extracted)
            return InsufficientData($"PagoParaNoGenerarIntereses is {ps.PagoParaNoGenerarIntereses.Status}.");

        // Confidence guard (Story 9.5) — check each confidence-bearing input field.
        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        // AdeudoPeriodoAnterior + PagosYAbonos may be zero-suppressed rows (Banamex omits zero-value
        // RESUMEN lines). Treat genuine absence (NotExtracted) as 0 ONLY when grounded (RC1.S4.a —
        // the label is found somewhere in the text layer); abstain if PRESENT but low-confidence,
        // if the label was found but the amount is unreadable (InvalidFormat — F1 honesty fix: a
        // printed-but-unreadable amount must not be silently substituted by 0m), or if NotExtracted
        // and ungrounded (real corpus: the row may be a non-zero, image-rendered amount).
        var normalizedFullText = ctx.StatementModel!.NormalizedFullText;

        decimal adeudoValue = 0m;
        if (ps.AdeudoPeriodoAnterior.Status == ExtractionStatus.ExtractedInvalidFormat)
            return InsufficientData(
                "AdeudoPeriodoAnterior amount present but unparseable; cannot imply zero.");
        if (ps.AdeudoPeriodoAnterior.Status == ExtractionStatus.Extracted)
        {
            if (ctx.ConfidenceBelowThreshold(ps.AdeudoPeriodoAnterior, confidenceThreshold))
                return InsufficientData(ConfidenceGuard.Reason(
                    "AdeudoPeriodoAnterior", ps.AdeudoPeriodoAnterior.Confidence, confidenceThreshold));
            adeudoValue = ps.AdeudoPeriodoAnterior.Value;
        }
        else if (ps.AdeudoPeriodoAnterior.Status == ExtractionStatus.NotExtracted
            && !IsZeroSuppressionGrounded(normalizedFullText, ZeroSuppressionLabels.AdeudoPeriodoAnterior))
        {
            return InsufficientData(
                "AdeudoPeriodoAnterior is NotExtracted and its RESUMEN label was not found anywhere " +
                "in the text layer; cannot confirm implied zero (RC1.S4.a — the row may be a " +
                "non-zero, image-rendered amount rather than a genuinely zero-suppressed row).");
        }

        decimal pagosValue = 0m;
        if (ps.PagosYAbonos.Status == ExtractionStatus.ExtractedInvalidFormat)
            return InsufficientData(
                "PagosYAbonos amount present but unparseable; cannot imply zero.");
        if (ps.PagosYAbonos.Status == ExtractionStatus.Extracted)
        {
            if (ctx.ConfidenceBelowThreshold(ps.PagosYAbonos, confidenceThreshold))
                return InsufficientData(ConfidenceGuard.Reason(
                    "PagosYAbonos", ps.PagosYAbonos.Confidence, confidenceThreshold));
            pagosValue = ps.PagosYAbonos.Value;
        }
        else if (ps.PagosYAbonos.Status == ExtractionStatus.NotExtracted
            && !IsZeroSuppressionGrounded(normalizedFullText, ZeroSuppressionLabels.PagosYAbonos))
        {
            return InsufficientData(
                "PagosYAbonos is NotExtracted and its RESUMEN label was not found anywhere in the " +
                "text layer; cannot confirm implied zero (RC1.S4.a — the row may be a non-zero, " +
                "image-rendered amount rather than a genuinely zero-suppressed row).");
        }

        if (ctx.ConfidenceBelowThreshold(ps.CargosRegularesNoMeses, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "CargosRegularesNoMeses", ps.CargosRegularesNoMeses.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.CargosComprasAMesesCapital, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "CargosComprasAMesesCapital", ps.CargosComprasAMesesCapital.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.MontoIntereses, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "MontoIntereses", ps.MontoIntereses.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.MontoComisiones, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "MontoComisiones", ps.MontoComisiones.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.IvaInteresesYComisiones, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "IvaInteresesYComisiones", ps.IvaInteresesYComisiones.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.PagoParaNoGenerarIntereses, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "PagoParaNoGenerarIntereses", ps.PagoParaNoGenerarIntereses.Confidence, confidenceThreshold));

        // CL-21 formula — AdeudoPeriodoAnterior and PagosYAbonos use implied-zero local variables.
        var computed =
            adeudoValue
            + ps.CargosRegularesNoMeses.Value
            + ps.CargosComprasAMesesCapital.Value
            + ps.MontoIntereses.Value
            + ps.MontoComisiones.Value
            + ps.IvaInteresesYComisiones.Value
            - pagosValue;

        var observed = ps.PagoParaNoGenerarIntereses.Value;
        var diff = Math.Abs(computed - observed);

        var locator = ps.PagoParaNoGenerarIntereses.Locator;
        var legalPasses = diff <= legalTolerance;
        var tenantPasses = diff <= effectiveTolerance;

        if (tenantPasses)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"{observed:F2}",
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
                expected: $"{computed:F2}",
                observed: $"{observed:F2}",
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

    /// <summary>
    /// RC1.S4.a grounding check for the implied-zero branch: returns <see langword="true"/> when
    /// <paramref name="label"/> (the operand's CONDUSEF RESUMEN label text, already normalized —
    /// uppercase, accent-free) is found anywhere in <paramref name="normalizedFullText"/>. This is
    /// the narrowest available honest signal that the row was at least partially typeset (as
    /// opposed to wholly image-rendered) — see the "Guarded implied-zero" remarks on this type for
    /// the real-corpus evidence that motivated this gate.
    /// </summary>
    private static bool IsZeroSuppressionGrounded(string normalizedFullText, string label) =>
        !string.IsNullOrEmpty(normalizedFullText)
        && normalizedFullText.Contains(label, StringComparison.Ordinal);
}
