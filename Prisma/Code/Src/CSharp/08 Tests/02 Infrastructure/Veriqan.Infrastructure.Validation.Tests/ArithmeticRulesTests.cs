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
        // Tolerance in fraction space: 0.50/100000 = 0.000005 — very tight
        // Set extractedCat = exactly computed fraction
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
        // Computed fraction ≈ 0.2125; set extracted to 0.3000 (way off)
        const decimal tasa = 0.1975m;
        var rule = GetRule("CL-10");
        var ps = MakeSummary(cat: Found(0.3000m), tasa: Found(tasa));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    // -----------------------------------------------------------------------
    // CL-18, CL-19, CL-20, CL-23 — always InsufficientData (Story 4.4 pending)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("CL-18")]
    [InlineData("CL-19")]
    [InlineData("CL-20")]
    [InlineData("CL-23")]
    public void Cl18_19_20_23_AlwaysReturnsInsufficientData(string checkId)
    {
        var rule = GetRule(checkId);
        // Even with ToleranceConfig and a full StatementModel these rules emit InsufficientData
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
            $"{checkId} must emit InsufficientData until Story 4.4 table extraction is done.");
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

    [Fact]
    public void Cl21_AnyInputFieldMissing_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-21");
        // PagosYAbonos is Missing — should short-circuit to InsufficientData
        var ps = MakeSummary(
            pagoParaNoGenerarIntereses: Found(32446.69m),
            adeudoPeriodoAnterior: Found(67796.35m),
            cargosRegularesNoMeses: Found(31461.30m),
            cargosComprasAMesesCapital: Found(985.39m),
            montoIntereses: Found(0m),
            montoComisiones: Found(0m),
            ivaInteresesYComisiones: Found(0m),
            pagosYAbonos: null);   // ← null → Missing
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
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
    // CL-26 tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl26_CreditoDisponibleExtracted_ReturnsPass()
    {
        var rule = GetRule("CL-26");
        var ps = MakeSummary(creditoDisponible: Found(47612.15m));
        var ctx = Ctx(BundleWithAccount(), ModelWith(ps));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.CheckId.ShouldBe("CL-26");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
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
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("CL-21")]
    [InlineData("CL-22")]
    [InlineData("CL-24")]
    [InlineData("CL-25")]
    [InlineData("CL-26")]
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
    /// have all their inputs available (CL-21, CL-22, CL-24, CL-25, CL-26) should Pass;
    /// the table-pending rules (CL-18/19/20/23) should InsufficientData;
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

        // CL-18/19/20/23: InsufficientData (table pending Story 4.4)
        foreach (var id in new[] { "CL-18", "CL-19", "CL-20", "CL-23" })
            findings.Single(f => f.CheckId == id).Verdict
                .ShouldBe(FindingVerdict.InsufficientData, $"{id} should be InsufficientData");

        // CL-21, CL-22, CL-24, CL-25, CL-26: Pass (fixture-consistent)
        foreach (var id in new[] { "CL-21", "CL-22", "CL-24", "CL-25", "CL-26" })
            findings.Single(f => f.CheckId == id).Verdict
                .ShouldBe(FindingVerdict.Pass, $"{id} should Pass with fixture-consistent values");
    }
}
