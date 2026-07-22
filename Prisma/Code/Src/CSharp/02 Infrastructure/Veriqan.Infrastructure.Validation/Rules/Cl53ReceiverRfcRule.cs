using System.Text.RegularExpressions;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-53: Validates that the receiver (receptor) RFC extracted from the CFDI fiscal block
/// is present and matches the Mexican RFC pattern <c>^[A-ZÑ&amp;]{3,4}\d{6}[A-Z0-9]{3}$</c>.
/// </summary>
/// <remarks>
/// <para>
/// When the fiscal block is not required and not present, this rule returns Pass (n/a).
/// </para>
/// <para>
/// <b>RC1.S4.b (2026-07-22) — NotExtracted vs. ExtractedInvalidFormat:</b> when the extractor
/// found no receiver-RFC value at all (label-anchored lookup came up empty), this rule returns
/// <b>InsufficientData</b> rather than Fail. Real-corpus evidence (Banamex/Citibanamex) showed
/// the receiver's RFC is genuinely never printed in the fiscal-block page's text layer — it is
/// image-rendered elsewhere on the statement — so a hard Fail here would be an ungrounded false
/// positive, not a proven compliance gap. When a value WAS extracted but does not match the RFC
/// pattern, the rule still Fails (a malformed extracted value is a real defect, distinct from an
/// honest "not printed here"). This mirrors the established NotExtracted/ExtractedInvalidFormat
/// split used elsewhere in the checklist (e.g. CL-21's guarded implied-zero).
/// </para>
/// </remarks>
internal sealed class Cl53ReceiverRfcRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    private static readonly Regex RfcPattern = new(
        @"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public string CheckId => "CL-53";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §4";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var sm = ctx.StatementModel;
        if (sm is null)
            return InsufficientData("StatementModel is not populated.");

        var fb = sm.FiscalBlock;
        if (fb is null)
            return InsufficientData("FiscalBlock extraction has not run (header-only extraction).");

        var fiscalRequired = Cl50FiscalQrRule.IsFiscalRequired(fb, sm.PeriodSummary);

        if (!fiscalRequired && !fb.BlockPresent)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: "n/a — no fiscal block required on this statement",
                    locator: fb.Locator));
        }

        var rfc = fb.ReceiverRfc;

        if (!string.IsNullOrWhiteSpace(rfc) && RfcPattern.IsMatch(rfc))
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: rfc,
                    locator: fb.Locator));
        }

        // Not extracted at all (label-anchored lookup found no value) — abstain rather than
        // false-FAIL. See remarks: real-corpus evidence shows this field is genuinely never
        // printed in this page's text layer on this statement family.
        if (string.IsNullOrWhiteSpace(rfc))
        {
            return InsufficientData(
                "Receiver RFC not present in the fiscal-block page text layer "
                + "(commonly image-rendered elsewhere on the statement) — cannot confirm "
                + "compliance without visual/OCR confirmation.");
        }

        // A value WAS extracted but does not match the RFC pattern — a genuine defect.
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: @"Receiver RFC matching ^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$",
                observed: rfc,
                locator: fb.Locator));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
