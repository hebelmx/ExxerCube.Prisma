using System;
using System.Collections.Generic;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit tests for the Story 4.2 arithmetic validation rules:
/// CL-10, CL-18, CL-19, CL-20, CL-21, CL-22, CL-23, CL-24, CL-25, CL-26.
/// </summary>
/// <remarks>
/// All rules under test are discovered via Scrutor DI (the same path used in production).
/// Tests construct minimal <see cref="PeriodSummary"/> / <see cref="VerificationContext"/>
/// objects to drive specific code paths.
/// </remarks>
public sealed class ArithmeticRulesTests
{
    private const decimal Tol = 0.50m;   // standard CurrencyToleranceMxn
    private const decimal CreditLine = 100_000m;
    private const string ProductId = "TC-ARITH-TEST";

    // -----------------------------------------------------------------------
    // Shared fixture helpers
    // -----------------------------------------------------------------------

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static ToleranceConfig Tolerance() =>
        new(CurrencyToleranceMxn: Tol, PointsTolerance: null,
            RewardsPesosToleranceMxn: null, PointsToPesosExchangeRate: null);

    /// <summary>
    /// Bundle with ToleranceConfig and a single client account carrying the test credit line.
    /// </summary>
    private static VecReferenceBundle BundleWithAccount(decimal creditLine = CreditLine) =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product()],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts:
            [
                new ClientAccount(
                    ClientId: "C-001",
                    ClientName: null,
                    Rfc: null,
                    ClientNumber: null,
                    Address: null,
                    Accounts:
                    [
                        new AccountEntry(
                            AccountRef: "REF-001",
                            ProductId: ProductId,
                            CardNumber: null,
                            Clabe: null,
                            BranchNumber: null,
                            CreditLine: creditLine,
                            AccountOpenDate: null)
                    ])
            ],
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: Tolerance(),
            ValidationConstants: null);

    /// <summary>Bundle with ToleranceConfig but WITHOUT any ClientAccounts.</summary>
    private static VecReferenceBundle BundleNoAccount() =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product()],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: Tolerance(),
            ValidationConstants: null);

    /// <summary>Bundle with NO ToleranceConfig (all ADR-V3 rules → InsufficientData).</summary>
    private static VecReferenceBundle BundleNoTolerance() =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product()],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    /// <summary>
    /// Creates an <see cref="ExtractedField{T}"/> that represents a successfully extracted
    /// decimal value at page 1.
    /// </summary>
    private static ExtractedField<decimal> Found(decimal value) =>
        ExtractedField<decimal>.Found(value, P1());

    /// <summary>
    /// Creates an <see cref="ExtractedField{T}"/> representing a missing (not extracted) decimal field.
    /// </summary>
    private static ExtractedField<decimal> Missing() =>
        ExtractedField<decimal>.Missing(P1());

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> wrapping the supplied <see cref="PeriodSummary"/>.
    /// The header identity fields are all set to Missing strings (not relevant for arithmetic rules).
    /// </summary>
    private static StatementModel ModelWith(PeriodSummary ps)
    {
        var missingStr = ExtractedField<string>.Missing(P1());
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());
        return new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            PeriodSummary = ps
        };
    }

    /// <summary>
    /// Builds a <see cref="PeriodSummary"/> whose "mandatory" non-RESUMEN fields are all filled
    /// with plausible fixture values; the RESUMEN / NIVEL-DE-USO fields come from the caller.
    /// </summary>
    private static PeriodSummary MakeSummary(
        ExtractedField<decimal>? cat = null,
        ExtractedField<decimal>? tasa = null,
        ExtractedField<decimal>? pagoParaNoGenerarIntereses = null,
        ExtractedField<decimal>? pagoMinimo = null,
        ExtractedField<decimal>? pagoMinimoMasMeses = null,
        ExtractedField<decimal>? saldoDeudorTotal = null,
        ExtractedField<decimal>? creditoDisponible = null,
        ExtractedField<decimal>? adeudoPeriodoAnterior = null,
        ExtractedField<decimal>? cargosRegularesNoMeses = null,
        ExtractedField<decimal>? cargosComprasAMesesCapital = null,
        ExtractedField<decimal>? montoIntereses = null,
        ExtractedField<decimal>? montoComisiones = null,
        ExtractedField<decimal>? ivaInteresesYComisiones = null,
        ExtractedField<decimal>? pagosYAbonos = null,
        ExtractedField<decimal>? saldoCargosRegulares = null,
        ExtractedField<decimal>? saldoCargosAMeses = null)
    {
        var missingDate = ExtractedField<DateOnly>.Missing(P1());
        var missingInt = ExtractedField<int>.Missing(P1());
        var missingStr = ExtractedField<string>.Missing(P1());
        var dayCount = new DayCountVerification(
            PrintedDays: 31,
            ComputedSpanDays: 30,
            IsConsistent: true);

        return new PeriodSummary(
            product: missingStr,
            periodStart: missingDate,
            periodCutDate: missingDate,
            paymentDueDate: missingDate,
            dayCountPrinted: missingInt,
            dayCount: dayCount,
            pagoParaNoGenerarIntereses: pagoParaNoGenerarIntereses ?? Missing(),
            pagoMinimo: pagoMinimo ?? Missing(),
            pagoMinimoMasMeses: pagoMinimoMasMeses ?? Missing(),
            tasa: tasa ?? Missing(),
            cat: cat ?? Missing(),
            saldoDeudorTotal: saldoDeudorTotal ?? Missing(),
            creditoDisponible: creditoDisponible ?? Missing(),
            adeudoPeriodoAnterior: adeudoPeriodoAnterior,
            cargosRegularesNoMeses: cargosRegularesNoMeses,
            cargosComprasAMesesCapital: cargosComprasAMesesCapital,
            montoIntereses: montoIntereses,
            montoComisiones: montoComisiones,
            ivaInteresesYComisiones: ivaInteresesYComisiones,
            pagosYAbonos: pagosYAbonos,
            saldoCargosRegulares: saldoCargosRegulares,
            saldoCargosAMeses: saldoCargosAMeses);
    }

    private static VerificationContext Ctx(VecReferenceBundle bundle, StatementModel? model = null) =>
        new(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: bundle.ToleranceConfig,
            statementModel: model);

    private static IVecValidationRule GetRule(string checkId)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();
        var rules = sp.GetServices<IVecValidationRule>();
        foreach (var r in rules)
            if (r.CheckId == checkId) return r;
        throw new InvalidOperationException($"Rule {checkId} not found in DI.");
    }

    // -----------------------------------------------------------------------
    // CL-10 tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl10_NullToleranceConfig_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-10");
        var ctx = Ctx(BundleNoTolerance());

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-10");
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl10_CatFieldNotExtracted_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-10");
        var ps = MakeSummary(
            cat: Missing(),           // ← not extracted
            tasa: Found(0.1975m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl10_TasaFieldNotExtracted_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-10");
        var ps = MakeSummary(
            cat: Found(0.2886m),
            tasa: Missing());          // ← not extracted
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl10_NoCreditLineInBundle_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-10");
        var ps = MakeSummary(cat: Found(0.29m), tasa: Found(0.1975m));
        var ctx = Ctx(BundleNoAccount(), ModelWith(ps));  // no account → no credit line

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl10_CatWithinTolerance_ReturnsPass()
    {
        // Formula: CAT% = ((creditLine × tasa + 1500) / creditLine) × 100
        // = ((100000 × 0.1975 + 1500) / 100000) × 100
        // = (19750 + 1500) / 100000 × 100 = 21250/100000*100 = 21.25%
        // As fraction: 0.2125
        // New tolerance: percentage-point space, ±0.50 pct-pt.
        // Set extractedCat = exactly computed fraction (diff = 0 pct-pts → Pass).
        const decimal tasa = 0.1975m;
        const decimal commission = 1500m;
        var computedCatPercent = ((CreditLine * tasa + commission) / CreditLine) * 100m;
        var computedCatFraction = computedCatPercent / 100m;

        var rule = GetRule("CL-10");
        var ps = MakeSummary(cat: Found(computedCatFraction), tasa: Found(tasa));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-10");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    [Fact]
    public void Cl10_CatBeyondTolerance_ReturnsFail()
    {
        // Computed fraction ≈ 0.2125 (21.25%); set extracted to 0.3000 (30.00%)
        // Diff = 8.75 pct-pts >> 0.50 → Fail.
        const decimal tasa = 0.1975m;
        var rule = GetRule("CL-10");
        var ps = MakeSummary(cat: Found(0.3000m), tasa: Found(tasa));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    /// <summary>
    /// Boundary-discriminating test: extracted CAT differs from computed by ~0.30 pct-pt.
    /// Under the owner-ruled percentage-point tolerance (±0.50 pct-pt) this is within band → Pass.
    /// Under the old fraction-space tolerance (±0.50/100000 = 0.000005) this would have FAILED.
    /// </summary>
    [Fact]
    public void Cl10_CatDiffersBy030PctPt_ReturnsPass_NewToleranceSemantics()
    {
        // Computed: CAT% = ((100000 × 0.1975 + 1500) / 100000) × 100 = 21.25%
        // Extracted: 21.25% + 0.30 pct-pt = 21.55% → as fraction: 0.2155
        // Diff = 0.30 pct-pt ≤ 0.50 pct-pt → Pass (owner ruling).
        const decimal tasa = 0.1975m;
        const decimal commission = 1500m;
        var computedCatPercent = ((CreditLine * tasa + commission) / CreditLine) * 100m; // 21.25
        var extractedCatFraction = (computedCatPercent + 0.30m) / 100m;               // 0.2155

        var rule = GetRule("CL-10");
        var ps = MakeSummary(cat: Found(extractedCatFraction), tasa: Found(tasa));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "0.30 pct-pt difference is within the ±0.50 pct-pt owner-ruled tolerance");
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    /// <summary>
    /// Boundary-discriminating test: extracted CAT differs from computed by ~0.80 pct-pt.
    /// Under the owner-ruled percentage-point tolerance (±0.50 pct-pt) this is outside band → Fail.
    /// </summary>
    [Fact]
    public void Cl10_CatDiffersBy080PctPt_ReturnsFail_NewToleranceSemantics()
    {
        // Computed: CAT% = ((100000 × 0.1975 + 1500) / 100000) × 100 = 21.25%
        // Extracted: 21.25% + 0.80 pct-pt = 22.05% → as fraction: 0.2205
        // Diff = 0.80 pct-pt > 0.50 pct-pt → Fail.
        const decimal tasa = 0.1975m;
        const decimal commission = 1500m;
        var computedCatPercent = ((CreditLine * tasa + commission) / CreditLine) * 100m; // 21.25
        var extractedCatFraction = (computedCatPercent + 0.80m) / 100m;               // 0.2205

        var rule = GetRule("CL-10");
        var ps = MakeSummary(cat: Found(extractedCatFraction), tasa: Found(tasa));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "0.80 pct-pt difference exceeds the ±0.50 pct-pt owner-ruled tolerance");
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    // -----------------------------------------------------------------------
    // CL-23 — always InsufficientData (pending cross-period data)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("CL-23")]
    public void Cl23_AlwaysReturnsInsufficientData(string checkId)
    {
        var rule = GetRule(checkId);
        // CL-23 requires cross-period installment data not yet available.
        var ps = MakeSummary(
            adeudoPeriodoAnterior: Found(100m),
            cargosRegularesNoMeses: Found(200m),
            saldoCargosRegulares: Found(300m),
            saldoCargosAMeses: Found(150m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(checkId);
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            $"{checkId} must emit InsufficientData (cross-period data pending).");
    }

    // -----------------------------------------------------------------------
    // CL-18, CL-19, CL-20 — InsufficientData when movements not extracted
    // (Story 4.4: these rules now compute real sums; no movements → InsufficientData)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("CL-18")]
    [InlineData("CL-19")]
    [InlineData("CL-20")]
    public void Cl18_19_20_ReturnsInsufficientData_WhenNoMovements(string checkId)
    {
        var rule = GetRule(checkId);
        // StatementModel has no movements (MovementsStatus = SectionNotFound by default)
        var ps = MakeSummary(
            cargosRegularesNoMeses: Found(200m),
            cargosComprasAMesesCapital: Found(100m),
            pagosYAbonos: Found(50m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(checkId);
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            $"{checkId} must emit InsufficientData when DESGLOSE movements are not extracted.");
    }

    // -----------------------------------------------------------------------
    // CL-21 tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// The fixture from 01+Dummie+VEC+jul_ago+20252.pdf confirms:
    /// 67796.35 + 31461.30 + 985.39 + 0 + 0 + 0 - 67796.35 = 32446.69 = PagoParaNoGenerarIntereses
    /// </summary>
    [Fact]
    public void Cl21_FixtureValues_ReturnsPass()
    {
        var rule = GetRule("CL-21");
        var ps = MakeSummary(
            pagoParaNoGenerarIntereses: Found(32446.69m),
            adeudoPeriodoAnterior: Found(67796.35m),
            cargosRegularesNoMeses: Found(31461.30m),
            cargosComprasAMesesCapital: Found(985.39m),
            montoIntereses: Found(0m),
            montoComisiones: Found(0m),
            ivaInteresesYComisiones: Found(0m),
            pagosYAbonos: Found(67796.35m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.CheckId.ShouldBe("CL-21");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    [Fact]
    public void Cl21_NullToleranceConfig_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-21");
        var ctx = Ctx(BundleNoTolerance());

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    /// <summary>
    /// A missing CORE operand (MontoIntereses) must still short-circuit to InsufficientData,
    /// regardless of whether AdeudoPeriodoAnterior or PagosYAbonos are present.
    /// (AdeudoPeriodoAnterior and PagosYAbonos are no longer hard-guarded — their absence
    /// is treated as implied zero by the guarded-implied-zero rule change, Epic 5.)
    /// </summary>
    [Fact]
    public void Cl21_CoreOperandMissing_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-21");
        // MontoIntereses is Missing — core operand absent signals real extraction failure.
        var ps = MakeSummary(
            pagoParaNoGenerarIntereses: Found(32446.69m),
            adeudoPeriodoAnterior: Found(67796.35m),
            cargosRegularesNoMeses: Found(31461.30m),
            cargosComprasAMesesCapital: Found(985.39m),
            montoIntereses: null,          // ← null → Missing (core operand)
            montoComisiones: Found(0m),
            ivaInteresesYComisiones: Found(0m),
            pagosYAbonos: Found(67796.35m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // CL-21 guarded implied-zero tests (Epic 5)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Implied-zero FAIL: AdeudoPeriodoAnterior and PagosYAbonos are absent (not extracted);
    /// both are treated as 0. The five core operands plus target are extracted. The target is
    /// inflated beyond tolerance — rule must fire Fail.
    /// </summary>
    [Fact]
    public void Cl21_ImpliedZero_AdeudoAndPagosAbsent_TargetInflated_ReturnsFail()
    {
        var rule = GetRule("CL-21");
        // computed = 0 + 5000 + 2500 + 1000 + 500 + 100 - 0 = 9100.00
        // target   = 9101.00  →  diff = 1.00 > 0.50 (Tol) → Fail
        var ps = MakeSummary(
            pagoParaNoGenerarIntereses: Found(9101.00m), // injected bad value
            adeudoPeriodoAnterior: null,                 // ← absent → implied 0
            cargosRegularesNoMeses: Found(5000.00m),
            cargosComprasAMesesCapital: Found(2500.00m),
            montoIntereses: Found(1000.00m),
            montoComisiones: Found(500.00m),
            ivaInteresesYComisiones: Found(100.00m),
            pagosYAbonos: null);                         // ← absent → implied 0
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-21");
        result.Value.Verdict.ShouldBe(FindingVerdict.Fail,
            "implied-zero path must evaluate the formula and detect the arithmetic error");
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    /// <summary>
    /// Implied-zero PASS: AdeudoPeriodoAnterior and PagosYAbonos are absent; both implied 0.
    /// Target matches the sum of the five core operands exactly → Pass.
    /// </summary>
    [Fact]
    public void Cl21_ImpliedZero_AdeudoAndPagosAbsent_TargetMatchesSum_ReturnsPass()
    {
        var rule = GetRule("CL-21");
        // computed = 0 + 5000 + 2500 + 1000 + 500 + 100 - 0 = 9100.00
        // target   = 9100.00  →  diff = 0 < 0.50 → Pass
        var ps = MakeSummary(
            pagoParaNoGenerarIntereses: Found(9100.00m),
            adeudoPeriodoAnterior: null,                  // ← absent → implied 0
            cargosRegularesNoMeses: Found(5000.00m),
            cargosComprasAMesesCapital: Found(2500.00m),
            montoIntereses: Found(1000.00m),
            montoComisiones: Found(500.00m),
            ivaInteresesYComisiones: Found(100.00m),
            pagosYAbonos: null);                          // ← absent → implied 0
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-21");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass,
            "implied-zero path: zero-suppressed Adeudo/Pagos rows + correct target → Pass");
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    /// <summary>
    /// Block-not-located: target (PagoParaNoGenerarIntereses) is not extracted.
    /// The payment block was not found — rule must return InsufficientData regardless
    /// of whether Adeudo/Pagos are absent.
    /// </summary>
    [Fact]
    public void Cl21_TargetNotExtracted_AdeudoPagosAbsent_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-21");
        var ps = MakeSummary(
            pagoParaNoGenerarIntereses: null,            // ← absent → target not located
            adeudoPeriodoAnterior: null,                 // ← absent
            cargosRegularesNoMeses: Found(5000.00m),
            cargosComprasAMesesCapital: Found(2500.00m),
            montoIntereses: Found(1000.00m),
            montoComisiones: Found(500.00m),
            ivaInteresesYComisiones: Found(100.00m),
            pagosYAbonos: null);                         // ← absent
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "absent target means the payment block was not located — must abstain");
    }

    /// <summary>
    /// Present-but-low-confidence PagosYAbonos: the field IS extracted (Status == Extracted)
    /// but its confidence is below the threshold. The rule must abstain (InsufficientData)
    /// because a misread digit in a non-zero Pagos row could corrupt the formula silently.
    /// </summary>
    [Fact]
    public void Cl21_PagosExtractedBelowConfidenceThreshold_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-21");
        // Confidence 0.5 < threshold 0.8 (LegalMinFieldConfidenceDefault)
        var lowConfidencePagos = new ExtractedField<decimal>(
            5000.00m, 0.5, P1(), ExtractionStatus.Extracted);
        var ps = MakeSummary(
            pagoParaNoGenerarIntereses: Found(9100.00m),
            adeudoPeriodoAnterior: null,                  // ← absent → implied 0
            cargosRegularesNoMeses: Found(5000.00m),
            cargosComprasAMesesCapital: Found(2500.00m),
            montoIntereses: Found(1000.00m),
            montoComisiones: Found(500.00m),
            ivaInteresesYComisiones: Found(100.00m),
            pagosYAbonos: lowConfidencePagos);            // ← present but low-confidence
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "present but low-confidence PagosYAbonos must cause abstention");
    }

    /// <summary>
    /// Present-but-low-confidence AdeudoPeriodoAnterior: the field IS extracted (Status == Extracted)
    /// but its confidence is below the threshold. The rule must abstain (InsufficientData)
    /// because a misread digit in a non-zero Adeudo row could corrupt the formula silently.
    /// PagosYAbonos is absent (implied 0) to isolate the Adeudo guard.
    /// </summary>
    [Fact]
    public void Cl21_AdeudoExtractedBelowConfidenceThreshold_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-21");
        // Confidence 0.5 < threshold 0.8 (LegalMinFieldConfidenceDefault)
        var lowConfidenceAdeudo = new ExtractedField<decimal>(
            5000.00m, 0.5, P1(), ExtractionStatus.Extracted);
        var ps = MakeSummary(
            pagoParaNoGenerarIntereses: Found(9100.00m),
            adeudoPeriodoAnterior: lowConfidenceAdeudo,      // ← present but low-confidence
            cargosRegularesNoMeses: Found(5000.00m),
            cargosComprasAMesesCapital: Found(2500.00m),
            montoIntereses: Found(1000.00m),
            montoComisiones: Found(500.00m),
            ivaInteresesYComisiones: Found(100.00m),
            pagosYAbonos: null);                             // ← absent → implied 0
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "present but low-confidence AdeudoPeriodoAnterior must cause abstention");
    }

    [Fact]
    public void Cl21_ComputedDiffersByMoreThanTolerance_ReturnsFail()
    {
        var rule = GetRule("CL-21");
        // Computed = 67796.35 + 31461.30 + 985.39 + 0 + 0 + 0 - 67796.35 = 32446.69
        // Printed = 30000.00 (wrong by > 0.50)
        var ps = MakeSummary(
            pagoParaNoGenerarIntereses: Found(30000.00m),
            adeudoPeriodoAnterior: Found(67796.35m),
            cargosRegularesNoMeses: Found(31461.30m),
            cargosComprasAMesesCapital: Found(985.39m),
            montoIntereses: Found(0m),
            montoComisiones: Found(0m),
            ivaInteresesYComisiones: Found(0m),
            pagosYAbonos: Found(67796.35m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    // -----------------------------------------------------------------------
    // CL-22 tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl22_FixtureValues_SaldoEqualsPago_ReturnsPass()
    {
        var rule = GetRule("CL-22");
        // Fixture: SaldoCargosRegulares = 32446.69, PagoParaNoGenerarIntereses = 32446.69
        var ps = MakeSummary(
            pagoParaNoGenerarIntereses: Found(32446.69m),
            saldoCargosRegulares: Found(32446.69m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Cl22_NullToleranceConfig_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-22");
        var ctx = Ctx(BundleNoTolerance());

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl22_SaldoDiffersByMoreThanTolerance_ReturnsFail()
    {
        var rule = GetRule("CL-22");
        var ps = MakeSummary(
            pagoParaNoGenerarIntereses: Found(32446.69m),
            saldoCargosRegulares: Found(30000.00m));   // wrong by > 0.50
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    // -----------------------------------------------------------------------
    // CL-24 tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl24_FixtureValues_SaldoDeudorTotalEqualsSum_ReturnsPass()
    {
        // SaldoCargosRegulares (32446.69) + SaldoCargosAMeses (19941.16) = 52387.85
        var rule = GetRule("CL-24");
        var ps = MakeSummary(
            saldoDeudorTotal: Found(52387.85m),
            saldoCargosRegulares: Found(32446.69m),
            saldoCargosAMeses: Found(19941.16m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Cl24_SaldoDeudorTotalDeviates_ReturnsFail()
    {
        var rule = GetRule("CL-24");
        var ps = MakeSummary(
            saldoDeudorTotal: Found(50000.00m),   // wrong: sum is 52387.85
            saldoCargosRegulares: Found(32446.69m),
            saldoCargosAMeses: Found(19941.16m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
    }

    [Fact]
    public void Cl24_NullToleranceConfig_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-24");
        var ctx = Ctx(BundleNoTolerance());

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // CL-25 tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl25_FixtureValues_CreditoDisponibleMatchesFormula_ReturnsPass()
    {
        // creditLine(100000) - saldoDeudorTotal(52387.85) = 47612.15
        var rule = GetRule("CL-25");
        var ps = MakeSummary(
            saldoDeudorTotal: Found(52387.85m),
            creditoDisponible: Found(47612.15m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    [Fact]
    public void Cl25_CreditoDisponibleDeviates_ReturnsFail()
    {
        var rule = GetRule("CL-25");
        var ps = MakeSummary(
            saldoDeudorTotal: Found(52387.85m),
            creditoDisponible: Found(45000.00m));  // wrong
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
    }

    [Fact]
    public void Cl25_NoCreditLineInBundle_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-25");
        var ps = MakeSummary(
            saldoDeudorTotal: Found(52387.85m),
            creditoDisponible: Found(47612.15m));
        var ctx = Ctx(BundleNoAccount(), ModelWith(ps));   // no credit line

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl25_NullToleranceConfig_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-25");
        var ctx = Ctx(BundleNoTolerance());

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // CL-26 tests (owner ruling: always InsufficientData — adversarial review finding)
    // -----------------------------------------------------------------------

    /// <summary>
    /// CL-26 must return InsufficientData even when CreditoDisponible is extracted,
    /// because the separate "crédito disponible para disposiciones de efectivo" field is not
    /// yet extracted — comparing CreditoDisponible to itself is a tautology and is disallowed.
    /// Owner ruling applied during Epic 4 adversarial review.
    /// </summary>
    [Fact]
    public void Cl26_CreditoDisponibleExtracted_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-26");
        var ps = MakeSummary(creditoDisponible: Found(47612.15m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.CheckId.ShouldBe("CL-26");
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "CL-26 must not emit a tautological Pass; efectivo field is not separately extracted");
        result.Value.Observed.ShouldNotBeNullOrEmpty(
            "InsufficientData must carry a reason (in Observed) explaining the missing efectivo field");
    }

    [Fact]
    public void Cl26_CreditoDisponibleNotExtracted_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-26");
        var ps = MakeSummary(creditoDisponible: Missing());
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl26_NullToleranceConfig_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-26");
        var ctx = Ctx(BundleNoTolerance());

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // ToleranceApplied contract (ADR-V3) — checked on a sampling of rules
    // Note: CL-26 is intentionally excluded — it always returns InsufficientData
    // (owner ruling: tautology fix — separate efectivo field not yet extracted).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("CL-21")]
    [InlineData("CL-22")]
    [InlineData("CL-24")]
    [InlineData("CL-25")]
    public void AllArithmeticRules_WhenPassing_RecordToleranceApplied(string checkId)
    {
        // Build a context where the named rule can Pass
        // (use fixture-consistent values so the arithmetic holds)
        IVecValidationRule rule = GetRule(checkId);

        var ps = MakeSummary(
            pagoParaNoGenerarIntereses: Found(32446.69m),
            saldoDeudorTotal: Found(52387.85m),
            creditoDisponible: Found(47612.15m),
            adeudoPeriodoAnterior: Found(67796.35m),
            cargosRegularesNoMeses: Found(31461.30m),
            cargosComprasAMesesCapital: Found(985.39m),
            montoIntereses: Found(0m),
            montoComisiones: Found(0m),
            ivaInteresesYComisiones: Found(0m),
            pagosYAbonos: Found(67796.35m),
            saldoCargosRegulares: Found(32446.69m),
            saldoCargosAMeses: Found(19941.16m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue($"{checkId} evaluate failed: {result.Error}");
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            $"{checkId} should Pass with fixture-consistent values");
        result.Value.ToleranceApplied.ShouldBe(Tol,
            $"ADR-V3: {checkId} must record the applied tolerance, not a magic number");
    }

    // -----------------------------------------------------------------------
    // Engine-level end-to-end: all 10 rules run against fixture context
    // -----------------------------------------------------------------------

    /// <summary>
    /// Runs the full engine with fixture-consistent values; the arithmetic rules that
    /// have all their inputs available (CL-21, CL-22, CL-24, CL-25) should Pass;
    /// CL-26 is always InsufficientData (owner ruling: tautology fix — efectivo field not extracted);
    /// CL-18/19/20 now compute real DESGLOSE sums but the context has no movements
    /// (MovementsStatus = SectionNotFound) → InsufficientData;
    /// CL-23 is always InsufficientData (COMPRAS-A-MESES table extraction unscheduled);
    /// CL-10 needs CAT+TASA which are left Missing here → InsufficientData.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task Engine_FixtureContext_ArithmeticRulesYieldExpectedVerdicts()
    {
        var ct = TestContext.Current.CancellationToken;

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        await using var sp = services.BuildServiceProvider();
        var engine = sp.GetRequiredService<IVecValidationEngine>();

        var ps = MakeSummary(
            // CL-10 inputs absent → InsufficientData
            cat: Missing(),
            tasa: Missing(),
            // CL-21/22/24/25/26 inputs present with fixture-consistent values
            pagoParaNoGenerarIntereses: Found(32446.69m),
            adeudoPeriodoAnterior: Found(67796.35m),
            cargosRegularesNoMeses: Found(31461.30m),
            cargosComprasAMesesCapital: Found(985.39m),
            montoIntereses: Found(0m),
            montoComisiones: Found(0m),
            ivaInteresesYComisiones: Found(0m),
            pagosYAbonos: Found(67796.35m),
            saldoCargosRegulares: Found(32446.69m),
            saldoCargosAMeses: Found(19941.16m),
            saldoDeudorTotal: Found(52387.85m),
            creditoDisponible: Found(47612.15m));

        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));
        var result = await engine.RunAsync(ctx, ct);

        result.IsSuccess.ShouldBeTrue($"Engine failed: {result.Error}");
        var findings = result.Value!;

        // CL-10: InsufficientData (no CAT/TASA)
        findings.Single(f => f.CheckId == "CL-10").Verdict
            .ShouldBe(FindingVerdict.InsufficientData);

        // CL-18/19/20: InsufficientData — compute real sums but context has no movements (SectionNotFound)
        // CL-23: InsufficientData — COMPRAS-A-MESES table extraction unscheduled
        // CL-26: InsufficientData — owner ruling: separate efectivo field not extracted (tautology fix)
        foreach (var id in new[] { "CL-18", "CL-19", "CL-20", "CL-23", "CL-26" })
            findings.Single(f => f.CheckId == id).Verdict
                .ShouldBe(FindingVerdict.InsufficientData, $"{id} should be InsufficientData");

        // CL-21, CL-22, CL-24, CL-25: Pass (fixture-consistent)
        foreach (var id in new[] { "CL-21", "CL-22", "CL-24", "CL-25" })
            findings.Single(f => f.CheckId == id).Verdict
                .ShouldBe(FindingVerdict.Pass, $"{id} should Pass with fixture-consistent values");
    }
}
