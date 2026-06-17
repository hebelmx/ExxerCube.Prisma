using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-51: Validates that the fiscal code (folio fiscal / UUID CFDI) is present and non-empty
/// when the CFDI fiscal block is required.
/// </summary>
/// <remarks>
/// <para>
/// When the fiscal block is not required and not present, this rule returns Pass (n/a).
/// When the fiscal block is present but no fiscal code could be extracted, the rule Fails.
/// </para>
/// </remarks>
internal sealed class Cl51FiscalCodeRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-51";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

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

        // Block present or required — fiscal code must be non-empty.
        var hasCode = !string.IsNullOrWhiteSpace(fb.FiscalCode);

        if (hasCode)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: fb.FiscalCode,
                    locator: fb.Locator));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: "Non-empty fiscal code (folio fiscal / UUID CFDI) on fiscal block page",
                observed: "Fiscal code not extracted from page text",
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
