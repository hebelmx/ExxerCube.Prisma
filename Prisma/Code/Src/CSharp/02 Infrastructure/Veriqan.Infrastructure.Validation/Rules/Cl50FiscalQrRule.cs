using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-50: Validates that the fiscal (CFDI) block QR code is present and decodable when required.
/// </summary>
/// <remarks>
/// <para>
/// <b>Required gating:</b> the fiscal block is REQUIRED when the statement has commissions or IVA
/// (<c>PeriodSummary.MontoComisiones</c> or <c>IvaInteresesYComisiones</c> extracted and &gt; 0),
/// OR when the block was already found on the page (<see cref="FiscalBlock.BlockPresent"/> == true).
/// </para>
/// <para>
/// <b>Verdicts:</b>
/// <list type="bullet">
///   <item><b>Required + QR decodable:</b> Pass.</item>
///   <item><b>Required + QR not decodable:</b> Fail (Critical).</item>
///   <item><b>Not required + block absent:</b> Pass (n/a — no fiscal block required).</item>
///   <item><b>No StatementModel or PeriodSummary:</b> InsufficientData.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl50FiscalQrRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-50";

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

        var ps = sm.PeriodSummary;

        // Determine whether the fiscal block is required.
        var fiscalRequired = IsFiscalRequired(fb, ps);

        if (!fiscalRequired && !fb.BlockPresent)
        {
            // Not required and not present — this is fine; not a failure.
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: "n/a — no fiscal block required (no commissions/IVA on this statement)",
                    locator: fb.Locator));
        }

        // Block is present or required — QR must be decodable.
        if (fb.QrDecoded)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"QR decoded; payload length={fb.QrPayload?.Length ?? 0}",
                    locator: fb.Locator));
        }

        // Required or present but QR not decoded — Fail.
        var reason = fiscalRequired
            ? "Fiscal block is required (commissions/IVA > 0) but QR code is not decodable."
            : "Fiscal block page is present but QR code could not be decoded.";

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: "Decodable QR code on CFDI fiscal block page",
                observed: reason,
                locator: fb.Locator));
    }

    /// <summary>
    /// Returns <see langword="true"/> when the fiscal block is required for this statement.
    /// Required when: block is already present OR the statement has commissions/IVA &gt; 0.
    /// </summary>
    internal static bool IsFiscalRequired(FiscalBlock fb, PeriodSummary? ps)
    {
        if (fb.BlockPresent)
            return true;

        if (ps is null)
            return false;

        var hasComisiones = ps.MontoComisiones.Status == ExtractionStatus.Extracted
                            && ps.MontoComisiones.Value > 0m;
        var hasIva = ps.IvaInteresesYComisiones.Status == ExtractionStatus.Extracted
                     && ps.IvaInteresesYComisiones.Value > 0m;

        return hasComisiones || hasIva;
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
