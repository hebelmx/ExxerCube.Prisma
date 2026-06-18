using System.Collections.Generic;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit and integration tests for the Story 11.4 §6 payment-simulation recursion rule
/// (<c>LAW-§6-SIMULACION</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tests are entirely synthetic.</b> No §6 table extractor exists (Story 11.1 scope
/// carry-forward), so there are no real PDF fixtures with a §6 table. Instead, each test
/// directly constructs a <see cref="FinancialTable"/> with <see cref="SectionNumber"/> = 6
/// and injects it into a <see cref="StatementModel"/>, then drives the rule deterministically.
/// </para>
/// <para>
/// <b>§6 column semantic order (2 value cells per scenario row):</b>
/// [0] months-to-pay, [1] total ordinary (pre-IVA) interest.
/// Three rows: scenario k=1, k=2, k=5 (payment = k × PagoMinimo).
/// </para>
/// <para>
/// <b>Recursion formula per month:</b>
/// <c>interes = B × (Tasa/12)</c>, <c>iva = interes × 0.16</c>,
/// <c>B_next = B + interes + iva − payment</c>. Accumulate pre-IVA <c>interes</c> only.
/// Stop when B_next ≤ 0.
/// </para>
/// <para>
/// <b>Tolerance:</b> months ±1 (fixed); interest within CurrencyToleranceMxn = 0.50 MXN.
/// </para>
/// </remarks>
public sealed class Section6SimulationRuleTests
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    private const decimal CurrencyTol = 0.50m;  // CurrencyToleranceMxn legal default
    private const string ProductId = "TC-S6-TEST";
    private const string CheckId = "LAW-§6-SIMULACION";

    // -----------------------------------------------------------------------
    // Shared helpers
    // -----------------------------------------------------------------------

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static ToleranceConfig Tolerance() =>
        new(CurrencyToleranceMxn: CurrencyTol, PointsTolerance: null,
            RewardsPesosToleranceMxn: null, PointsToPesosExchangeRate: null);

    private static VecReferenceBundle BundleWithTolerance() =>
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

    private static VerificationContext Ctx(StatementModel? model = null) =>
        new(
            bundle: BundleWithTolerance(),
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(BundleWithTolerance()),
            priorStatement: null,
            toleranceConfig: BundleWithTolerance().ToleranceConfig,
            statementModel: model);

    private static IVecValidationRule GetRule()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();
        foreach (var r in sp.GetServices<IVecValidationRule>())
            if (r.CheckId == CheckId) return r;
        throw new System.InvalidOperationException($"Rule {CheckId} not found in DI.");
    }

    /// <summary>
    /// Builds a §6 <see cref="FinancialTable"/> with the provided scenario rows.
    /// </summary>
    private static FinancialTable MakeSection6Table(IReadOnlyList<TableRow> rows) =>
        new(
            sectionNumber: 6,
            sectionName: "¿Cuánto pagarías?",
            status: TableExtractionStatus.Extracted,
            rows: rows,
            locator: P1());

    /// <summary>
    /// Creates a single §6 scenario row with [months, interest] value cells.
    /// </summary>
    private static TableRow MakeScenarioRow(string label, decimal months, decimal interest) =>
        new(
            TableCell.LabelCell(label, P1()),
            new List<TableCell>
            {
                TableCell.Amount(months, months.ToString("F0"), P1()),
                TableCell.Amount(interest, interest.ToString("F2"), P1())
            });

    /// <summary>
    /// Builds a complete <see cref="PeriodSummary"/> with the three recursion inputs
    /// set to Extracted; all other fields are Missing.
    /// </summary>
    private static PeriodSummary MakePeriodSummary(
        decimal pagoParaNoGenerarIntereses,
        decimal pagoMinimo,
        decimal tasa)
    {
        var missingDate = ExtractedField<DateOnly>.Missing(P1());
        var missingInt = ExtractedField<int>.Missing(P1());
        var missingStr = ExtractedField<string>.Missing(P1());
        var missingDec = ExtractedField<decimal>.Missing(P1());
        var dayCount = new DayCountVerification(PrintedDays: 30, ComputedSpanDays: 30, IsConsistent: true);

        return new PeriodSummary(
            product: missingStr,
            periodStart: missingDate,
            periodCutDate: missingDate,
            paymentDueDate: missingDate,
            dayCountPrinted: missingInt,
            dayCount: dayCount,
            pagoParaNoGenerarIntereses: ExtractedField<decimal>.Found(pagoParaNoGenerarIntereses, P1()),
            pagoMinimo: ExtractedField<decimal>.Found(pagoMinimo, P1()),
            pagoMinimoMasMeses: missingDec,
            tasa: ExtractedField<decimal>.Found(tasa, P1()),
            cat: missingDec,
            saldoDeudorTotal: missingDec,
            creditoDisponible: missingDec);
    }

    /// <summary>
    /// Builds a <see cref="StatementModel"/> with the given §6 table and PeriodSummary.
    /// </summary>
    private static StatementModel ModelWith(FinancialTable table6, PeriodSummary periodSummary)
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
            FinancialTables = [table6],
            PeriodSummary = periodSummary
        };
    }

    // -----------------------------------------------------------------------
    // Rule metadata contract
    // -----------------------------------------------------------------------

    [Fact]
    public void Rule_Metadata_HasExpectedCheckIdAndClassification()
    {
        var rule = GetRule();
        rule.CheckId.ShouldBe(CheckId);
        rule.DofNumeral.ShouldBe("Acuerdo §6");
        rule.Classification.ShouldBe(RuleClassification.TenantTightenableOnly);
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }

    // -----------------------------------------------------------------------
    // (a) Recursion helper — unit tests for PaymentSimulation.Run
    // -----------------------------------------------------------------------

    /// <summary>
    /// Hand-verified recursion: B₀=2000, annual rate=0.24 (monthly r=0.02), IVA=0.16, P=500.
    /// <code>
    /// Month 1: i=40.00,        iva=6.40,        B=1546.40,  accum=40.00
    /// Month 2: i=30.928,       iva=4.94848,     B=1082.276, accum=70.928
    /// Month 3: i=21.645530,    iva=3.463285,    B=607.385,  accum=92.573530
    /// Month 4: i=12.147706,    iva=1.943633,    B=121.477,  accum=104.721236
    /// Month 5: i= 2.429533,    iva=0.387725,    B=negative, accum=107.150769
    /// </code>
    /// Expected: 5 months, totalOrdinaryInterest ≈ 107.15 MXN.
    /// </summary>
    [Fact]
    public void PaymentSimulation_KnownInputs_ReturnsExpectedMonthsAndInterest()
    {
        const decimal b0 = 2000m;
        const decimal annualRate = 0.24m;   // 24% annual → 2% monthly
        const decimal payment = 500m;

        var result = PaymentSimulation.Run(b0, annualRate, payment);

        result.IsAmortising.ShouldBeTrue();
        result.Months.ShouldBe(5);
        // totalOrdinaryInterest = 40 + 30.928 + 21.64553 + 12.14771 + 2.42953 = 107.15077
        result.TotalOrdinaryInterest.ShouldBeInRange(106.65m, 107.65m,
            "interest should be ~107.15 MXN within ±0.50 tolerance band");
    }

    /// <summary>
    /// Non-amortising: payment (20) ≤ first-month interes+IVA (20×0.02 + 20×0.02×0.16 = 0.4+0.064 → wait…)
    /// B₀=1000, r=0.02/month, first_interes=20, first_iva=3.2, total=23.2.
    /// Payment=10 &lt; 23.2 → non-amortising.
    /// </summary>
    [Fact]
    public void PaymentSimulation_PaymentBelowFirstMonthCharges_ReturnsNonAmortising()
    {
        const decimal b0 = 1000m;
        const decimal annualRate = 0.24m;   // monthly r = 0.02
        // first_interes = 1000 × 0.02 = 20, first_iva = 20 × 0.16 = 3.2
        // total first-month charges = 23.2; payment must be ≤ 23.2 to trigger non-amortising
        const decimal payment = 10m;

        var result = PaymentSimulation.Run(b0, annualRate, payment);

        result.IsAmortising.ShouldBeFalse();
    }

    /// <summary>
    /// Payment exactly equals first-month charges (20 = 20 when IVA is excluded),
    /// but 20 ≤ 23.2 → non-amortising.
    /// </summary>
    [Fact]
    public void PaymentSimulation_PaymentEqualsFirstMonthInterestExcludingIva_ReturnsNonAmortising()
    {
        const decimal b0 = 1000m;
        const decimal annualRate = 0.24m;   // monthly r = 0.02, first_interes = 20
        // Payment = 20 = first_interes, but first_interes + first_iva = 23.2
        // 20 ≤ 23.2 → non-amortising
        const decimal payment = 20m;

        var result = PaymentSimulation.Run(b0, annualRate, payment);

        result.IsAmortising.ShouldBeFalse();
    }

    /// <summary>
    /// Payment strictly above first-month charges → amortises.
    /// B₀=1000, r=0.02/month, first charges = 23.2; payment = 25 &gt; 23.2 → amortising.
    /// </summary>
    [Fact]
    public void PaymentSimulation_PaymentAboveFirstMonthCharges_ReturnsAmortising()
    {
        const decimal b0 = 1000m;
        const decimal annualRate = 0.24m;   // first charges = 23.2
        const decimal payment = 25m;       // > 23.2

        var result = PaymentSimulation.Run(b0, annualRate, payment);

        result.IsAmortising.ShouldBeTrue();
        result.Months.ShouldBeGreaterThan(0);
        result.TotalOrdinaryInterest.ShouldBeGreaterThan(0m);
    }

    /// <summary>
    /// Zero balance: B₀ ≤ 0 is handled at rule level; recursion is never called with
    /// B₀ ≤ 0 in normal flow. But if called with a positive very-small balance that pays
    /// off month 1, it should still return 1 month.
    /// </summary>
    [Fact]
    public void PaymentSimulation_SmallBalancePaysOffFirstMonth_Returns1Month()
    {
        const decimal b0 = 10m;
        const decimal annualRate = 0.12m;   // monthly r = 0.01
        // first charges = 10 × 0.01 + (10×0.01×0.16) = 0.10 + 0.016 = 0.116
        const decimal payment = 50m;       // >> 0.116 → balance gone month 1

        var result = PaymentSimulation.Run(b0, annualRate, payment);

        result.IsAmortising.ShouldBeTrue();
        result.Months.ShouldBe(1);
        result.TotalOrdinaryInterest.ShouldBeInRange(0.09m, 0.11m, "~0.10 MXN interest for month 1");
    }

    // -----------------------------------------------------------------------
    // (b) Full rule: synthetic §6 table whose printed values match → Pass
    // -----------------------------------------------------------------------

    /// <summary>
    /// B₀=2000, PagoMinimo=200, Tasa=0.24.
    /// k=1: P=200 → simulation must amortise; k=2: P=400; k=5: P=1000.
    /// Build a §6 table with the exact computed months/interest for all three scenarios.
    /// The rule must return Pass.
    /// </summary>
    [Fact]
    public void Evaluate_PrintedValuesMatchRecursion_ReturnsPass()
    {
        const decimal b0 = 2000m;
        const decimal pagoMinimo = 200m;
        const decimal tasa = 0.24m;

        // Pre-compute via the same engine (white-box but deterministic — we own the formula)
        var sim1 = PaymentSimulation.Run(b0, tasa, 1 * pagoMinimo);
        var sim2 = PaymentSimulation.Run(b0, tasa, 2 * pagoMinimo);
        var sim5 = PaymentSimulation.Run(b0, tasa, 5 * pagoMinimo);

        sim1.IsAmortising.ShouldBeTrue("k=1 must amortise for this test to be meaningful");
        sim2.IsAmortising.ShouldBeTrue("k=2 must amortise");
        sim5.IsAmortising.ShouldBeTrue("k=5 must amortise");

        var rows = new List<TableRow>
        {
            MakeScenarioRow("Pago mínimo (k=1)", sim1.Months, sim1.TotalOrdinaryInterest),
            MakeScenarioRow("2× pago mínimo (k=2)", sim2.Months, sim2.TotalOrdinaryInterest),
            MakeScenarioRow("5× pago mínimo (k=5)", sim5.Months, sim5.TotalOrdinaryInterest)
        };

        var table6 = MakeSection6Table(rows);
        var summary = MakePeriodSummary(b0, pagoMinimo, tasa);
        var model = ModelWith(table6, summary);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(CheckId);
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(CurrencyTol);
    }

    /// <summary>
    /// Printed values are within the ±0.50 MXN / ±1 month tolerance → Pass.
    /// </summary>
    [Fact]
    public void Evaluate_PrintedValuesWithinTolerance_ReturnsPass()
    {
        const decimal b0 = 2000m;
        const decimal pagoMinimo = 200m;
        const decimal tasa = 0.24m;

        var sim1 = PaymentSimulation.Run(b0, tasa, 1 * pagoMinimo);
        var sim2 = PaymentSimulation.Run(b0, tasa, 2 * pagoMinimo);
        var sim5 = PaymentSimulation.Run(b0, tasa, 5 * pagoMinimo);

        // Nudge interest by 0.30 MXN (within 0.50 tolerance) on scenario k=1
        var rows = new List<TableRow>
        {
            MakeScenarioRow("k=1", sim1.Months, sim1.TotalOrdinaryInterest + 0.30m),
            MakeScenarioRow("k=2", sim2.Months, sim2.TotalOrdinaryInterest),
            MakeScenarioRow("k=5", sim5.Months, sim5.TotalOrdinaryInterest)
        };

        var table6 = MakeSection6Table(rows);
        var summary = MakePeriodSummary(b0, pagoMinimo, tasa);
        var ctx = Ctx(ModelWith(table6, summary));

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "0.30 MXN difference is within the ±0.50 MXN tolerance");
    }

    // -----------------------------------------------------------------------
    // (c) Printed values mismatch beyond tolerance → Fail citing §6
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_PrintedInterestMismatchBeyondTolerance_ReturnsFail()
    {
        const decimal b0 = 2000m;
        const decimal pagoMinimo = 200m;
        const decimal tasa = 0.24m;

        var sim1 = PaymentSimulation.Run(b0, tasa, 1 * pagoMinimo);
        var sim2 = PaymentSimulation.Run(b0, tasa, 2 * pagoMinimo);
        var sim5 = PaymentSimulation.Run(b0, tasa, 5 * pagoMinimo);

        // Scenario k=1: inflate interest by 50 MXN — way beyond 0.50 tolerance
        var rows = new List<TableRow>
        {
            MakeScenarioRow("k=1", sim1.Months, sim1.TotalOrdinaryInterest + 50m),
            MakeScenarioRow("k=2", sim2.Months, sim2.TotalOrdinaryInterest),
            MakeScenarioRow("k=5", sim5.Months, sim5.TotalOrdinaryInterest)
        };

        var table6 = MakeSection6Table(rows);
        var summary = MakePeriodSummary(b0, pagoMinimo, tasa);
        var ctx = Ctx(ModelWith(table6, summary));

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed.ShouldNotBeNullOrEmpty("Fail must carry a reason");
        result.Value.Observed.ShouldContain("§6", Case.Sensitive,
            "Fail reason must cite §6 / Banxico Circular 13/2011");
    }

    [Fact]
    public void Evaluate_PrintedMonthsMismatchBeyondOneTolerance_ReturnsFail()
    {
        const decimal b0 = 2000m;
        const decimal pagoMinimo = 200m;
        const decimal tasa = 0.24m;

        var sim1 = PaymentSimulation.Run(b0, tasa, 1 * pagoMinimo);
        var sim2 = PaymentSimulation.Run(b0, tasa, 2 * pagoMinimo);
        var sim5 = PaymentSimulation.Run(b0, tasa, 5 * pagoMinimo);

        // Scenario k=2: print months = computed + 3 (beyond ±1 tolerance)
        var rows = new List<TableRow>
        {
            MakeScenarioRow("k=1", sim1.Months, sim1.TotalOrdinaryInterest),
            MakeScenarioRow("k=2", sim2.Months + 3, sim2.TotalOrdinaryInterest),
            MakeScenarioRow("k=5", sim5.Months, sim5.TotalOrdinaryInterest)
        };

        var table6 = MakeSection6Table(rows);
        var summary = MakePeriodSummary(b0, pagoMinimo, tasa);
        var ctx = Ctx(ModelWith(table6, summary));

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    /// <summary>
    /// Months mismatch of exactly ±1 is within tolerance → Pass.
    /// </summary>
    [Fact]
    public void Evaluate_MonthsMismatchExactlyOne_ReturnsPass()
    {
        const decimal b0 = 2000m;
        const decimal pagoMinimo = 200m;
        const decimal tasa = 0.24m;

        var sim1 = PaymentSimulation.Run(b0, tasa, 1 * pagoMinimo);
        var sim2 = PaymentSimulation.Run(b0, tasa, 2 * pagoMinimo);
        var sim5 = PaymentSimulation.Run(b0, tasa, 5 * pagoMinimo);

        // k=1 months printed as computed+1 (±1 is within tolerance)
        var rows = new List<TableRow>
        {
            MakeScenarioRow("k=1", sim1.Months + 1, sim1.TotalOrdinaryInterest),
            MakeScenarioRow("k=2", sim2.Months, sim2.TotalOrdinaryInterest),
            MakeScenarioRow("k=5", sim5.Months, sim5.TotalOrdinaryInterest)
        };

        var table6 = MakeSection6Table(rows);
        var summary = MakePeriodSummary(b0, pagoMinimo, tasa);
        var ctx = Ctx(ModelWith(table6, summary));

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "±1 month is within the months tolerance");
    }

    // -----------------------------------------------------------------------
    // (d) Non-amortising scenario → InsufficientData (never infinite loop)
    // -----------------------------------------------------------------------

    /// <summary>
    /// PagoMinimo is set so small that k=1 payment does not cover first-month charges.
    /// B₀=5000, Tasa=0.60 (60% annual, monthly r=0.05), first_interes=250, first_iva=40, total=290.
    /// PagoMinimo=200 → k=1 payment=200 &lt; 290 → non-amortising → InsufficientData.
    /// </summary>
    [Fact]
    public void Evaluate_NonAmorisingScenario_ReturnsInsufficientData_NeverLoops()
    {
        const decimal b0 = 5000m;
        const decimal tasa = 0.60m;       // monthly r = 0.05
        const decimal pagoMinimo = 200m;  // k=1: payment=200; first charges=5000×0.05×1.16=290 → non-amortising

        // first_interes = 5000 × 0.05 = 250; first_iva = 250 × 0.16 = 40 → total = 290
        // 200 < 290 → non-amortising

        // We still need a §6 table (even with "wrong" values) — the rule hits the non-amortising
        // guard before it reads the printed cells.
        var rows = new List<TableRow>
        {
            MakeScenarioRow("k=1", 999m, 99999m),
            MakeScenarioRow("k=2", 999m, 99999m),
            MakeScenarioRow("k=5", 999m, 99999m)
        };

        var table6 = MakeSection6Table(rows);
        var summary = MakePeriodSummary(b0, pagoMinimo, tasa);
        var ctx = Ctx(ModelWith(table6, summary));

        // Must complete quickly (no infinite loop) and must return InsufficientData.
        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Non-amortising scenario must return InsufficientData, not Fail");
        result.Value.Observed.ShouldNotBeNullOrEmpty("reason must be present");
    }

    // -----------------------------------------------------------------------
    // (e) Low-confidence inputs → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_PagoMinimoLowConfidence_ReturnsInsufficientData()
    {
        var missingDate = ExtractedField<DateOnly>.Missing(P1());
        var missingInt = ExtractedField<int>.Missing(P1());
        var missingStr = ExtractedField<string>.Missing(P1());
        var missingDec = ExtractedField<decimal>.Missing(P1());
        var dayCount = new DayCountVerification(30, 30, true);

        // PagoMinimo has confidence 0.5 (below 0.8 threshold)
        var summary = new PeriodSummary(
            product: missingStr,
            periodStart: missingDate,
            periodCutDate: missingDate,
            paymentDueDate: missingDate,
            dayCountPrinted: missingInt,
            dayCount: dayCount,
            pagoParaNoGenerarIntereses: ExtractedField<decimal>.Found(2000m, P1()),
            pagoMinimo: new ExtractedField<decimal>(200m, 0.5, P1(), ExtractionStatus.Extracted),
            pagoMinimoMasMeses: missingDec,
            tasa: ExtractedField<decimal>.Found(0.24m, P1()),
            cat: missingDec,
            saldoDeudorTotal: missingDec,
            creditoDisponible: missingDec);

        var rows = new List<TableRow>
        {
            MakeScenarioRow("k=1", 5m, 107m),
            MakeScenarioRow("k=2", 3m, 80m),
            MakeScenarioRow("k=5", 1m, 20m)
        };

        var table6 = MakeSection6Table(rows);
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());
        var model = new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            FinancialTables = [table6],
            PeriodSummary = summary
        };

        var result = GetRule().Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Low-confidence PagoMinimo must cause abstain, not a verdict");
    }

    [Fact]
    public void Evaluate_TasaNotExtracted_ReturnsInsufficientData()
    {
        var missingDate = ExtractedField<DateOnly>.Missing(P1());
        var missingInt = ExtractedField<int>.Missing(P1());
        var missingStr = ExtractedField<string>.Missing(P1());
        var missingDec = ExtractedField<decimal>.Missing(P1());
        var dayCount = new DayCountVerification(30, 30, true);

        // Tasa not extracted
        var summary = new PeriodSummary(
            product: missingStr,
            periodStart: missingDate,
            periodCutDate: missingDate,
            paymentDueDate: missingDate,
            dayCountPrinted: missingInt,
            dayCount: dayCount,
            pagoParaNoGenerarIntereses: ExtractedField<decimal>.Found(2000m, P1()),
            pagoMinimo: ExtractedField<decimal>.Found(200m, P1()),
            pagoMinimoMasMeses: missingDec,
            tasa: ExtractedField<decimal>.Missing(P1()),  // not extracted
            cat: missingDec,
            saldoDeudorTotal: missingDec,
            creditoDisponible: missingDec);

        var rows = new List<TableRow>
        {
            MakeScenarioRow("k=1", 5m, 107m),
            MakeScenarioRow("k=2", 3m, 80m),
            MakeScenarioRow("k=5", 1m, 20m)
        };

        var table6 = MakeSection6Table(rows);
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());
        var model = new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            FinancialTables = [table6],
            PeriodSummary = summary
        };

        var result = GetRule().Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Not-extracted Tasa must cause abstain");
    }

    // -----------------------------------------------------------------------
    // (f) §6 table absent → InsufficientData (the real-world path today)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section6TableAbsent_ReturnsInsufficientData()
    {
        // Model has no §6 table (reflects real production state until §6 extractor is built)
        var missingStr = ExtractedField<string>.Missing(P1());
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());
        var model = new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            FinancialTables = [],   // no §6 table
            PeriodSummary = MakePeriodSummary(2000m, 200m, 0.24m)
        };

        var result = GetRule().Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Absent §6 table must return InsufficientData — never Fail");
        result.Value.Observed.ShouldNotBeNullOrEmpty("reason must be present");
    }

    [Fact]
    public void Evaluate_Section6TableSectionNotFound_ReturnsInsufficientData()
    {
        var missingStr = ExtractedField<string>.Missing(P1());
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());
        var model = new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            FinancialTables = [FinancialTable.NotFound(6, "¿Cuánto pagarías?")],
            PeriodSummary = MakePeriodSummary(2000m, 200m, 0.24m)
        };

        var result = GetRule().Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_Section6TableIndeterminate_ReturnsInsufficientData()
    {
        var missingStr = ExtractedField<string>.Missing(P1());
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());
        var model = new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            FinancialTables = [FinancialTable.Indeterminate(6, "¿Cuánto pagarías?", P1())],
            PeriodSummary = MakePeriodSummary(2000m, 200m, 0.24m)
        };

        var result = GetRule().Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var result = GetRule().Evaluate(Ctx(model: null), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_NullPeriodSummary_ReturnsInsufficientData()
    {
        // §6 table is present and Extracted, but PeriodSummary is null
        var missingStr = ExtractedField<string>.Missing(P1());
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());

        var rows = new List<TableRow>
        {
            MakeScenarioRow("k=1", 5m, 107m),
            MakeScenarioRow("k=2", 3m, 80m),
            MakeScenarioRow("k=5", 1m, 20m)
        };
        var table6 = MakeSection6Table(rows);

        var model = new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            FinancialTables = [table6],
            PeriodSummary = null   // no PeriodSummary
        };

        var result = GetRule().Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Missing PeriodSummary must abstain — recursion inputs unavailable");
    }

    // -----------------------------------------------------------------------
    // (g) Real-fixture integration: no §6 table extracted → InsufficientData (NEVER Fail)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mirrors the real-world production state: Story 11.1 did not extract §6, so the
    /// jul_ago (and all current) fixtures have no §6 FinancialTable entry.
    /// The rule MUST produce InsufficientData — NEVER a false Fail that would halt a billing run.
    /// </summary>
    [Fact]
    public void Evaluate_RealFixtureShape_NoSection6Table_ProducesInsufficientDataNeverFail()
    {
        // The real fixture has §8/§19/§20 tables but no §6 table.
        // We reproduce the relevant shape synthetically.
        var missingStr = ExtractedField<string>.Missing(P1());
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());

        // Include other-section tables (as in a real statement) but NO §6 table.
        var model = new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            FinancialTables =
            [
                FinancialTable.NotFound(8,  "Indicadores del costo anual de la tarjeta"),
                FinancialTable.NotFound(19, "Saldo sobre el que se calcularon los intereses del periodo"),
                FinancialTable.NotFound(20, "Distribución de tu último pago"),
                // §6 intentionally absent — matches real production state
            ],
            PeriodSummary = MakePeriodSummary(
                pagoParaNoGenerarIntereses: 32_446.69m,
                pagoMinimo: 2_160.00m,
                tasa: 0.2736m)
        };

        var result = GetRule().Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(CheckId);

        // Cardinal rule: NEVER a false Fail on a real statement.
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "The billing-run gate MUST NEVER false-Fail when §6 table is absent (production state today)");

        // Correct verdict is InsufficientData.
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Absent §6 table → InsufficientData is the correct abstain verdict");
    }

    // -----------------------------------------------------------------------
    // (h) Unregistered tolerance → InsufficientData (not exception)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_UnregisteredTolerance_ReturnsInsufficientData_NotException()
    {
        var rule = new Section6PaymentSimulationRule(new EmptyToleranceProvider());

        var rows = new List<TableRow>
        {
            MakeScenarioRow("k=1", 5m, 107m),
            MakeScenarioRow("k=2", 3m, 80m),
            MakeScenarioRow("k=5", 1m, 20m)
        };
        var table6 = MakeSection6Table(rows);
        var summary = MakePeriodSummary(2000m, 200m, 0.24m);
        var ctx = Ctx(ModelWith(table6, summary));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("must be success-wrapped, not an exception");
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Unregistered tolerance must return InsufficientData, never throw");
    }

    // -----------------------------------------------------------------------
    // Stub: empty tolerance provider
    // -----------------------------------------------------------------------

    private sealed class EmptyToleranceProvider : ILegalToleranceProvider
    {
        public bool Has(string checkId) => false;
        public Tolerance For(string checkId) =>
            throw new InvalidOperationException($"No tolerance registered for '{checkId}'.");
    }

    // -----------------------------------------------------------------------
    // Cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_CancellationRequested_ReturnsCancelled()
    {
        var rule = GetRule();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(Ctx(null), cts.Token);

        result.IsSuccess.ShouldBeFalse("Cancelled result must be a failure Result");
    }

    // -----------------------------------------------------------------------
    // ToleranceApplied contract
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_PassVerdict_RecordsToleranceApplied()
    {
        const decimal b0 = 2000m;
        const decimal pagoMinimo = 200m;
        const decimal tasa = 0.24m;

        var sim1 = PaymentSimulation.Run(b0, tasa, 1 * pagoMinimo);
        var sim2 = PaymentSimulation.Run(b0, tasa, 2 * pagoMinimo);
        var sim5 = PaymentSimulation.Run(b0, tasa, 5 * pagoMinimo);

        var rows = new List<TableRow>
        {
            MakeScenarioRow("k=1", sim1.Months, sim1.TotalOrdinaryInterest),
            MakeScenarioRow("k=2", sim2.Months, sim2.TotalOrdinaryInterest),
            MakeScenarioRow("k=5", sim5.Months, sim5.TotalOrdinaryInterest)
        };

        var table6 = MakeSection6Table(rows);
        var summary = MakePeriodSummary(b0, pagoMinimo, tasa);
        var ctx = Ctx(ModelWith(table6, summary));

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(CurrencyTol,
            "ADR-V3: the applied tolerance must be recorded on Pass findings");
    }
}
