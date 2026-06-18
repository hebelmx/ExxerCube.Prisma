using System;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-10: Validates the printed CAT (Costo Anual Total) against the formula:
/// <c>CAT = (((creditLine × tasaAnualOrdinariaFija) + annualCommission) / creditLine) × 100</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula (ADR-V2):</b>
/// <c>CAT = (((creditLine × tasa) + catAnnualCommissionMxn) / creditLine) × 100</c>
/// where <c>tasa</c> is the extracted (or bundle) annual ordinary fixed rate (decimal fraction),
/// <c>creditLine</c> is the resolved account's credit line from the bundle,
/// and <c>catAnnualCommissionMxn</c> defaults to 1500 when not supplied.
/// </para>
/// <para>
/// <b>Tolerance (ADR-V3, owner ruling):</b> resolved via <see cref="ILegalToleranceProvider"/>
/// using the bundle's <c>ToleranceConfig.CurrencyToleranceMxn</c> as the optional override.
/// The spec is <c>Tolerance(LegalDefault=0.50, Min=0.00, Max=1.00)</c> in
/// <b>percentage-point space</b>: <c>|extractedCatPercent − computedCatPercent| ≤ effective</c>.
/// The extracted <c>Cat</c> field is stored as a decimal fraction (e.g. 0.2886 = 28.86%);
/// it is multiplied by 100 before comparison.
/// <see cref="RuleFinding.ToleranceApplied"/> records the effective value.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item>Extracted CAT field is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item>Extracted TASA field is <see cref="ExtractionStatus.NotExtracted"/> (needed for formula).</item>
///   <item>No <c>ClientAccount</c> with an <c>AccountEntry</c> for the resolved product.</item>
///   <item>The resolved account's <c>AccountEntry.CreditLine</c> is null or zero.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl10CatRule : IVecValidationRule
{
    private const string Version = "1.0.0";
    private const decimal DefaultAnnualCommission = 1500m;

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Cl10CatRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Cl10CatRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "CL-10";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §9";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.TenantTightenableOnly;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Resolve tolerance — legal default applies when bundle has no override
        var resolution = _toleranceProvider.For(CheckId)
            .Resolve(ctx.ToleranceConfig?.CurrencyToleranceMxn);
        var tolerance = resolution.EffectiveValue;

        // StatementModel must be present
        var ps = ctx.StatementModel?.PeriodSummary;
        if (ps is null)
            return InsufficientData("StatementModel or PeriodSummary is not populated.");

        // Extracted CAT field
        if (ps.Cat.Status != ExtractionStatus.Extracted)
            return InsufficientData($"CAT field is {ps.Cat.Status}; cannot compare.");

        // TASA is needed for the formula
        if (ps.Tasa.Status != ExtractionStatus.Extracted)
            return InsufficientData($"TASA field is {ps.Tasa.Status}; cannot compute CAT formula.");

        var extractedCat = ps.Cat.Value;     // decimal fraction (e.g. 0.2886 for 28.86%)
        var tasa = ps.Tasa.Value;            // decimal fraction (e.g. 0.2736)

        // Resolve credit line from bundle
        var creditLine = ResolveCreditLine(ctx);
        if (creditLine is null)
            return InsufficientData("Credit line not found in bundle for the resolved product.");
        if (creditLine.Value == 0m)
            return InsufficientData("Credit line is zero; CAT formula requires a non-zero credit line.");

        // Annual commission — from bundle or default
        var annualCommission = ctx.Bundle.ValidationConstants?.CatAnnualCommissionMxn
            ?? DefaultAnnualCommission;

        // CAT formula: ((creditLine × tasa + annualCommission) / creditLine) × 100
        // The formula yields a percentage value (e.g. 28.86 for 28.86%).
        var computedCatPercent = ((creditLine.Value * tasa + annualCommission) / creditLine.Value) * 100m;

        // The extracted Cat field is stored as a decimal fraction (e.g. 0.2886 = 28.86%).
        // Convert to percentage points for comparison (owner ruling: tolerance is in pct-pt space).
        var extractedCatPercent = extractedCat * 100m;

        // Compare both in percentage-point space: |extractedCatPercent − computedCatPercent| ≤ 0.50
        var diff = Math.Abs(extractedCatPercent - computedCatPercent);

        var locator = ps.Cat.Locator;
        var expectedStr = $"{computedCatPercent:F4}%";
        var observedStr = $"{extractedCatPercent:F4}%";

        if (diff <= tolerance)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: observedStr,
                    toleranceApplied: tolerance,
                    locator: locator));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: expectedStr,
                observed: observedStr,
                toleranceApplied: tolerance,
                locator: locator));
    }

    /// <summary>
    /// Locates the credit line for the resolved product from the bundle's ClientAccounts section.
    /// Returns null when no matching account entry is found.
    /// </summary>
    private static decimal? ResolveCreditLine(VerificationContext ctx)
    {
        if (ctx.Bundle.ClientAccounts is null)
            return null;

        var productId = ctx.ResolvedProduct.ProductId;

        foreach (var client in ctx.Bundle.ClientAccounts)
        {
            if (client.Accounts is null)
                continue;

            foreach (var account in client.Accounts)
            {
                if (string.Equals(account.ProductId, productId, StringComparison.OrdinalIgnoreCase)
                    && account.CreditLine is not null)
                {
                    return account.CreditLine;
                }
            }
        }

        return null;
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}
