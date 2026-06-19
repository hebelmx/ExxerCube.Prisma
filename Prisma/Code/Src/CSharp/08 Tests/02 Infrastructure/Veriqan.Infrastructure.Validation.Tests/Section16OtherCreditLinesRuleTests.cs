using System;
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
/// Unit and integration tests for Story 11.6b §16 other-credit-lines rule
/// (full per-column verification — <c>LAW-§16-OTRASLINEAS</c>).
/// </summary>
/// <remarks>
/// <para>
/// §16 is a <b>conditional section</b>: it appears only when the cardholder has other
/// credit lines on the same statement. In ALL current fixtures the section is absent
/// (<see cref="TableExtractionStatus.SectionNotFound"/>), so the primary real-world
/// path is Pass with an N/A note — never Fail.
/// </para>
/// <para>
/// <b>Column semantic order (9 value cells, 0-indexed):</b>
/// [0] Fecha, [1] Descripción, [2] Monto original,
/// [3] Saldo pendiente, [4] Intereses del periodo, [5] IVA de intereses,
/// [6] Pago requerido, [7] Núm. de pago, [8] Tasa de interés aplicable.
/// </para>
/// <para>
/// <b>Checks:</b>
/// Check 1 — Intereses(v) ≈ SaldoPendiente(iv) × (Tasa(ix)/360) × días, within tolerance.
/// Check 2 — IVA(vi) ≈ |Intereses(v)| × 0.16, within tolerance.
/// Check 3 — §16 total Intereses ≈ §19 "otras líneas" Monto (best-effort; skipped when absent).
/// </para>
/// <para>
/// <b>NOTE — production §16 column-mapping accuracy is corpus-gated:</b>
/// no real §16 fixture exists to calibrate column positions. All tests here are
/// synthetic. Production accuracy is unverified until a real §16 corpus is obtained.
/// </para>
/// </remarks>
public sealed class Section16OtherCreditLinesRuleTests
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    private const decimal Tol = 0.50m;     // CurrencyToleranceMxn legal default
    private const string ProductId = "TC-S16-TEST";
    private const string CheckId = "LAW-§16-OTRASLINEAS";
    private const decimal IvaRate = 0.16m;
    private const decimal DaysPerYear = 360m;

    // §16 column indices (mirrors the rule's private constants)
    private const int ColSaldoPendiente = 3;
    private const int ColIntereses = 4;
    private const int ColIva = 5;
    private const int ColTasa = 8;
    private const int TotalCols = 9;

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
        new(CurrencyToleranceMxn: Tol, PointsTolerance: null,
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
        throw new InvalidOperationException($"Rule {CheckId} not found in DI.");
    }

    /// <summary>
    /// Builds a full 9-cell §16 row. Cells not explicitly provided default to NA.
    /// </summary>
    private static TableRow MakeRow(
        string label,
        decimal saldoPendiente,
        decimal intereses,
        decimal iva,
        decimal tasa,
        double interesConfidence = 1.0,
        double ivaConfidence = 1.0,
        double saldoConfidence = 1.0,
        double tasaConfidence = 1.0,
        CellKind tasaKind = CellKind.Rate)
    {
        // Build exactly 9 value cells in §16 column order
        var cells = new TableCell[TotalCols];
        for (var i = 0; i < TotalCols; i++)
            cells[i] = TableCell.NotApplicableCell("N/A", P1());

        cells[ColSaldoPendiente] = new TableCell(
            saldoPendiente.ToString("F2"), saldoPendiente, CellKind.Amount, saldoConfidence, P1());
        cells[ColIntereses] = new TableCell(
            intereses.ToString("F2"), intereses, CellKind.Amount, interesConfidence, P1());
        cells[ColIva] = new TableCell(
            iva.ToString("F2"), iva, CellKind.Amount, ivaConfidence, P1());
        cells[ColTasa] = new TableCell(
            tasa.ToString("F4"), tasa, tasaKind, tasaConfidence, P1());

        return new TableRow(TableCell.LabelCell(label, P1()), cells);
    }

    /// <summary>
    /// Builds a §16 <see cref="FinancialTable"/> with the given rows.
    /// </summary>
    private static FinancialTable MakeSection16Table(IReadOnlyList<TableRow> rows) =>
        new(
            sectionNumber: 16,
            sectionName: "Información de otras líneas de crédito",
            status: TableExtractionStatus.Extracted,
            rows: rows,
            locator: P1());

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> with the given financial tables
    /// and an optional <see cref="PeriodSummary"/> (for días + Check 3 cross-checks).
    /// </summary>
    private static StatementModel ModelWith(
        IReadOnlyList<FinancialTable> tables,
        PeriodSummary? periodSummary = null)
    {
        var missingStr  = ExtractedField<string>.Missing(P1());
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
            FinancialTables = tables,
            PeriodSummary = periodSummary
        };
    }

    /// <summary>
    /// Builds a minimal <see cref="PeriodSummary"/> with the given ComputedSpanDays and no other fields.
    /// </summary>
    private static PeriodSummary PeriodSummaryWithDays(int computedSpanDays)
    {
        var missingDate = ExtractedField<DateOnly>.Missing(P1());
        var missingInt  = ExtractedField<int>.Missing(P1());
        var missingStr  = ExtractedField<string>.Missing(P1());
        var missingDec  = ExtractedField<decimal>.Missing(P1());
        var dayCount    = new DayCountVerification(
            PrintedDays: computedSpanDays + 1,
            ComputedSpanDays: computedSpanDays,
            IsConsistent: true);

        return new PeriodSummary(
            product: missingStr,
            periodStart: missingDate,
            periodCutDate: missingDate,
            paymentDueDate: missingDate,
            dayCountPrinted: missingInt,
            dayCount: dayCount,
            pagoParaNoGenerarIntereses: missingDec,
            pagoMinimo: missingDec,
            pagoMinimoMasMeses: missingDec,
            tasa: missingDec,
            cat: missingDec,
            saldoDeudorTotal: missingDec,
            creditoDisponible: missingDec);
    }

    /// <summary>
    /// Builds a §19 table with a single "otras líneas de crédito" row whose Monto (col[3]) is
    /// the given amount. Used for Check 3 cross-check tests.
    /// </summary>
    private static FinancialTable MakeSection19TableWithOtrasLineas(decimal montoOtrasLineas)
    {
        // §19 column order: [0] SaldoBase, [1] Días, [2] Tasa, [3] Monto
        var cells = new TableCell[]
        {
            TableCell.Amount(0m, "0.00", P1()),                               // SaldoBase (not relevant)
            TableCell.Days(30m, "30", P1()),                                  // Días (not relevant)
            TableCell.Rate(0.20m, "20.00%", P1()),                            // Tasa (not relevant)
            TableCell.Amount(montoOtrasLineas, montoOtrasLineas.ToString("F2"), P1()) // Monto
        };
        var row = new TableRow(
            TableCell.LabelCell("Por disposiciones de efectivo de otras líneas de crédito", P1()),
            cells);

        return new FinancialTable(
            sectionNumber: 19,
            sectionName: "Saldo sobre el que se calcularon los intereses del periodo",
            status: TableExtractionStatus.Extracted,
            rows: [row],
            locator: P1());
    }

    // -----------------------------------------------------------------------
    // Rule metadata contract
    // -----------------------------------------------------------------------

    [Fact]
    public void Rule_Metadata_HasExpectedCheckIdAndClassification()
    {
        var rule = GetRule();
        rule.CheckId.ShouldBe(CheckId);
        rule.DofNumeral.ShouldBe("Acuerdo §16");
        rule.Classification.ShouldBe(RuleClassification.TenantTightenableOnly);
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }

    // -----------------------------------------------------------------------
    // (a) §16 SectionNotFound → Pass with N/A note (the real-fixture path)
    // -----------------------------------------------------------------------

    /// <summary>
    /// §16 table has status SectionNotFound: the cardholder has no other credit lines.
    /// This is the standard path for ALL current fixtures.
    /// Rule MUST return Pass with a note containing "N/A" — never Fail.
    /// </summary>
    [Fact]
    public void Evaluate_Section16SectionNotFound_ReturnsPassWithNaNote()
    {
        var table16 = FinancialTable.NotFound(16, "Información de otras líneas de crédito");
        var model = ModelWith([table16]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(CheckId);
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass,
            "SectionNotFound is not a defect — conditional section legitimately absent");
        result.Value.Observed!.ShouldContain("N/A");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Cardinal rule: never false-Fail when §16 is legitimately absent");
    }

    /// <summary>
    /// When the model has no §16 table at all (empty FinancialTables list),
    /// the rule must also return Pass with N/A note (treated same as SectionNotFound).
    /// </summary>
    [Fact]
    public void Evaluate_Section16TableAbsentFromModel_ReturnsPassWithNaNote()
    {
        var table20 = FinancialTable.NotFound(20, "Distribución de tu último pago");
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "No §16 table in model → treated as not applicable → Pass");
        result.Value.Observed!.ShouldContain("N/A");
    }

    // -----------------------------------------------------------------------
    // (b) §16 present, all rows reconcile → Pass
    // -----------------------------------------------------------------------

    /// <summary>
    /// §16 present with one row where:
    ///   Check 2 — IVA ≈ Intereses × 0.16 (exact).
    ///   Check 1 — días not available (PeriodSummary absent) → sub-check skipped.
    /// Overall verdict: Pass.
    /// </summary>
    [Fact]
    public void Evaluate_Section16PresentAllChecksPass_IvaExact_ReturnsPass()
    {
        const decimal interes = 500.00m;
        var ivaExpected = interes * IvaRate; // 80.00

        var row = MakeRow("Línea personal", saldoPendiente: 10_000m, intereses: interes,
            iva: ivaExpected, tasa: 0.2736m);

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]); // no PeriodSummary → días unavailable → Check 1 skipped
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "IVA check passes exactly; Check 1 skipped (no días) → Pass");
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    /// <summary>
    /// §16 present with days from PeriodSummary available, Check 1 and Check 2 both reconcile.
    /// Formula: Intereses = SaldoPendiente × (Tasa/360) × días.
    /// </summary>
    [Fact]
    public void Evaluate_Section16PresentCheck1AndCheck2Pass_ReturnsPass()
    {
        const decimal saldo = 5_000m;
        const decimal tasa = 0.20m;      // 20% as fraction
        const int days = 30;
        var interes = saldo * (tasa / DaysPerYear) * days;  // 5000×(0.20/360)×30 = 83.333…
        var iva = interes * IvaRate;

        var row = MakeRow("Crédito hipotecario", saldoPendiente: saldo,
            intereses: interes, iva: iva, tasa: tasa);

        var table = MakeSection16Table([row]);
        var ps = PeriodSummaryWithDays(days);
        var model = ModelWith([table], ps);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Check 1 (Intereses vs rate/días) and Check 2 (IVA vs Intereses) both reconcile");
    }

    /// <summary>
    /// Check 2 (IVA) within tolerance (difference = 0.30 ≤ 0.50) → Pass.
    /// </summary>
    [Fact]
    public void Evaluate_IvaWithinToleranceRounding_ReturnsPass()
    {
        const decimal interes = 500.00m;
        var ivaExpected = interes * IvaRate;            // 80.00
        var ivaWithRounding = ivaExpected + 0.30m;      // 80.30 — within 0.50 tolerance

        var row = MakeRow("Línea auto", saldoPendiente: 10_000m,
            intereses: interes, iva: ivaWithRounding, tasa: 0.20m);

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "IVA diff=0.30 is within tolerance=0.50 → Pass");
    }

    // -----------------------------------------------------------------------
    // (c) IVA-vs-interés mismatch beyond tolerance → Fail citing §16
    // -----------------------------------------------------------------------

    /// <summary>
    /// IVA reported is significantly wrong: expected 80.00, reported 200.00.
    /// diff = |200 - 80| = 120 > 0.50 → Fail.
    /// </summary>
    [Fact]
    public void Evaluate_IvaMismatchBeyondTolerance_ReturnsFail()
    {
        const decimal interes  = 500.00m;
        const decimal ivaWrong = 200.00m; // expected 80.00

        var row = MakeRow("Línea de crédito", saldoPendiente: 10_000m,
            intereses: interes, iva: ivaWrong, tasa: 0.20m);

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "IVA mismatch 120.00 >> tolerance 0.50 → Fail");
        result.Value.Observed.ShouldNotBeNullOrEmpty("Fail must include a reason citing §16");
        result.Value.Observed!.ShouldContain("IVA");
    }

    /// <summary>
    /// IVA just over the tolerance boundary: diff = 0.51 > 0.50 → Fail.
    /// </summary>
    [Fact]
    public void Evaluate_IvaDiffJustOverTolerance_ReturnsFail()
    {
        const decimal interes = 1_000.00m;
        var ivaExpected = interes * IvaRate;     // 160.00
        var ivaJustOver = ivaExpected + 0.51m;   // 160.51 — just over tolerance

        var row = MakeRow("Crédito auto", saldoPendiente: 20_000m,
            intereses: interes, iva: ivaJustOver, tasa: 0.18m);

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "IVA diff=0.51 > tolerance=0.50 → Fail");
    }

    // -----------------------------------------------------------------------
    // (d) interés-vs-rate/días mismatch → Fail
    // -----------------------------------------------------------------------

    /// <summary>
    /// Check 1 fails: reported Intereses is far off from the recomputed value.
    /// SaldoPendiente=5000, Tasa=20% fraction, días=30 → expected≈83.33.
    /// Reported=500.00, diff≈416.67 >> 0.50 → Fail.
    /// Check 2 (IVA) is set to be consistent with the reported Intereses so only Check 1 fires.
    /// </summary>
    [Fact]
    public void Evaluate_InteresVsRateDaysMismatch_ReturnsFail()
    {
        const decimal saldo = 5_000m;
        const decimal tasa = 0.20m;
        const int days = 30;
        // Correct expected interés: 5000×(0.20/360)×30 ≈ 83.33
        const decimal interesWrong = 500.00m; // reported far too high
        var ivaForReported = interesWrong * IvaRate; // IVA consistent with reported (so only Check 1 fires)

        var row = MakeRow("Crédito hipotecario", saldoPendiente: saldo,
            intereses: interesWrong, iva: ivaForReported, tasa: tasa);

        var table = MakeSection16Table([row]);
        var ps = PeriodSummaryWithDays(days);
        var model = ModelWith([table], ps);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "Intereses vs rate/días mismatch >> 0.50 → Fail (Check 1)");
        result.Value.Observed!.ShouldContain("Check 1"); // Fail reason must identify Check 1
    }

    // -----------------------------------------------------------------------
    // (e) Low-confidence or missing cells → InsufficientData (not Fail)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Intereses cell has low confidence (0.5 < 0.8 threshold) → InsufficientData.
    /// Cardinal rule: never false-Fail on low-confidence extraction.
    /// </summary>
    [Fact]
    public void Evaluate_InteresLowConfidence_ReturnsInsufficientData_NotFail()
    {
        const decimal interes = 500.00m;
        var iva = interes * IvaRate;

        // Build the row manually so we can set low confidence on Intereses
        var row = MakeRow("Línea personal",
            saldoPendiente: 10_000m, intereses: interes, iva: iva, tasa: 0.20m,
            interesConfidence: 0.5); // below threshold

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Low-confidence Intereses cell → InsufficientData (cardinal rule: never false-Fail)");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail);
    }

    /// <summary>
    /// IVA cell has low confidence (0.6 < 0.8 threshold) → InsufficientData.
    /// </summary>
    [Fact]
    public void Evaluate_IvaLowConfidence_ReturnsInsufficientData_NotFail()
    {
        const decimal interes = 500.00m;
        var iva = interes * IvaRate;

        var row = MakeRow("Línea auto",
            saldoPendiente: 10_000m, intereses: interes, iva: iva, tasa: 0.20m,
            ivaConfidence: 0.6); // below threshold

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Low-confidence IVA cell → InsufficientData");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail);
    }

    // -----------------------------------------------------------------------
    // (f) Row with too few columns → InsufficientData
    // -----------------------------------------------------------------------

    /// <summary>
    /// §16 table row has only 5 value cells (fewer than 9 required).
    /// Rule MUST return InsufficientData — cannot map columns reliably.
    /// Cardinal rule: never false-Fail when column mapping is uncertain.
    /// </summary>
    [Fact]
    public void Evaluate_Section16RowTooFewColumns_ReturnsInsufficientData()
    {
        var cells = new List<TableCell>
        {
            TableCell.Amount(500.00m, "500.00", P1()),
            TableCell.Amount(80.00m,  "80.00",  P1()),
            TableCell.Amount(10_000m, "10000.00", P1()),
            TableCell.Amount(0.20m,   "0.20",   P1()),
            TableCell.NotApplicableCell("N/A", P1())
        };
        var row = new TableRow(TableCell.LabelCell("Línea incompleta", P1()), cells);

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Row has fewer than 9 columns → cannot map columns reliably → InsufficientData");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Cardinal rule: never false-Fail when column mapping is uncertain");
    }

    // -----------------------------------------------------------------------
    // (g) Check 3 (totals-tie vs §19 otras-líneas) cross-check
    // -----------------------------------------------------------------------

    /// <summary>
    /// §19 "otras líneas" row present with matching Monto → Check 3 passes → overall Pass.
    /// §16 has one row with Intereses = 500.00.
    /// §19 otras-líneas Monto = 500.00 → diff = 0 → cross-check passes.
    /// </summary>
    [Fact]
    public void Evaluate_Section16Check3TotalsTieMatch_ReturnsPass()
    {
        const decimal interes = 500.00m;
        var iva = interes * IvaRate;

        var row = MakeRow("Línea personal", saldoPendiente: 10_000m,
            intereses: interes, iva: iva, tasa: 0.20m);

        var table16 = MakeSection16Table([row]);
        var table19 = MakeSection19TableWithOtrasLineas(interes); // §19 Monto matches §16 Intereses

        var model = ModelWith([table16, table19]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "§19 otras-líneas Monto matches §16 total Intereses → Check 3 passes → Pass");
    }

    /// <summary>
    /// §19 "otras líneas" row present but Monto disagrees by 150.00 >> tolerance 0.50.
    /// Check 3 fires → Fail.
    /// </summary>
    [Fact]
    public void Evaluate_Section16Check3TotalsTieMismatch_ReturnsFail()
    {
        const decimal interes = 500.00m;
        var iva = interes * IvaRate;

        var row = MakeRow("Línea personal", saldoPendiente: 10_000m,
            intereses: interes, iva: iva, tasa: 0.20m);

        var table16 = MakeSection16Table([row]);

        // §19 otras-líneas Monto is 350.00 — does NOT match §16 Intereses total of 500.00
        const decimal section19Monto = 350.00m;
        var table19 = MakeSection19TableWithOtrasLineas(section19Monto);

        var model = ModelWith([table16, table19]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "§16 total Intereses=500 vs §19 Monto=350, diff=150 >> tolerance 0.50 → Fail (Check 3)");
        result.Value.Observed!.ShouldContain("Check 3"); // Fail reason must identify Check 3
    }

    /// <summary>
    /// §19 "otras líneas" row absent → Check 3 is skipped → verdict is Pass based on Check 2 only.
    /// </summary>
    [Fact]
    public void Evaluate_Section16Check3CrossCheckAbsent_SkippedNotFail()
    {
        const decimal interes = 500.00m;
        var iva = interes * IvaRate;

        var row = MakeRow("Línea personal", saldoPendiente: 10_000m,
            intereses: interes, iva: iva, tasa: 0.20m);

        var table16 = MakeSection16Table([row]);
        // No §19 table in model → Check 3 is silently skipped
        var model = ModelWith([table16]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "No §19 table present → Check 3 is skipped (abstained); Check 2 passes → overall Pass");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Missing §19 cross-reference must never become a false Fail");
    }

    // -----------------------------------------------------------------------
    // §16 Indeterminate and NoRowsParsed → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section16StatusIndeterminate_ReturnsInsufficientData()
    {
        var table16 = FinancialTable.Indeterminate(
            16, "Información de otras líneas de crédito", P1());

        var model = ModelWith([table16]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Indeterminate table — cannot reliably verify arithmetic → InsufficientData");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Indeterminate must never become a false Fail");
    }

    [Fact]
    public void Evaluate_Section16StatusNoRowsParsed_ReturnsInsufficientData()
    {
        var table16 = FinancialTable.NoRows(
            16, "Información de otras líneas de crédito", P1());

        var model = ModelWith([table16]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "NoRowsParsed means the table cannot be verified → InsufficientData");
    }

    // -----------------------------------------------------------------------
    // All rows NA → InsufficientData (no row/sub-check could be evaluated)
    // -----------------------------------------------------------------------

    /// <summary>
    /// §16 extracted but all rows have NA cells for the needed columns.
    /// No check can be evaluated → InsufficientData (not Fail, not Pass).
    /// </summary>
    [Fact]
    public void Evaluate_Section16AllRowsNA_ReturnsInsufficientData()
    {
        // Build a 9-cell row where Intereses and IVA are both NA
        var cells = new TableCell[TotalCols];
        for (var i = 0; i < TotalCols; i++)
            cells[i] = TableCell.NotApplicableCell("N/A", P1());

        var row = new TableRow(TableCell.LabelCell("Línea inactiva", P1()), cells);
        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "All rows NA → no check evaluated → InsufficientData");
    }

    // -----------------------------------------------------------------------
    // Unregistered tolerance → InsufficientData (not exception)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_UnregisteredTolerance_ReturnsInsufficientData_NotException()
    {
        var rule = new Section16OtherCreditLinesRule(new EmptyToleranceProvider());

        const decimal interes = 500.00m;
        var iva = interes * IvaRate;

        var row = MakeRow("Línea de crédito", saldoPendiente: 10_000m,
            intereses: interes, iva: iva, tasa: 0.20m);
        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("must be success-wrapped, not an exception");
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Unregistered tolerance must return InsufficientData, never throw");
    }

    // -----------------------------------------------------------------------
    // Null StatementModel → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var result = GetRule().Evaluate(Ctx(model: null), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // Cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_CancellationRequested_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = GetRule().Evaluate(Ctx(null), cts.Token);

        result.IsSuccess.ShouldBeFalse("Cancelled result must be a failure Result");
    }

    // -----------------------------------------------------------------------
    // (h) Real-fixture integration: jul_ago §16 absent → Pass (N/A), never Fail
    // -----------------------------------------------------------------------

    /// <summary>
    /// Constructs a synthetic model that mirrors the jul_ago fixture state:
    /// §16 is absent (SectionNotFound) because the statement has no other credit lines.
    /// The rule MUST produce Pass with an N/A note — and explicitly MUST NOT be Fail.
    ///
    /// NOTE: production §16 column-mapping accuracy is corpus-gated — no real §16 fixture
    /// exists to run end-to-end extraction against. This test validates the absent-section
    /// path which IS the real-fixture path for all known statements.
    /// </summary>
    [Fact]
    public void Evaluate_JulAgoFixtureShape_Section16Absent_ReturnsPassWithNaNeverFail()
    {
        // The real jul_ago fixture has §16 SectionNotFound — reproduced synthetically
        // so this test is deterministic and independent of a real PDF on disk.
        var table16 = FinancialTable.NotFound(16, "Información de otras líneas de crédito");
        var model = ModelWith([table16]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(CheckId);

        // The cardinal rule: a preventive billing-run gate MUST NEVER false-Fail.
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Cardinal rule: a billing-run gate MUST NEVER false-Fail when §16 is legitimately absent");

        // The correct verdict for the absent-conditional-section path is Pass with N/A.
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass,
            "§16 absent (SectionNotFound) on a statement without other credit lines → Pass (N/A)");

        result.Value.Observed!.ShouldContain("N/A");
    }

    // -----------------------------------------------------------------------
    // Rate normalization edge cases
    // -----------------------------------------------------------------------

    /// <summary>
    /// Tasa stored as percentage number (e.g. 20.00 not 0.20) with CellKind.Rate.
    /// Rule must normalize: 20.00 > 1.5 → divide by 100 → 0.20 fraction.
    /// Check 1 should still reconcile.
    /// </summary>
    [Fact]
    public void Evaluate_TasaStoredAsPercentageNumber_NormalizedCorrectly_ReturnsPass()
    {
        const decimal saldo = 5_000m;
        const decimal tasaPercentage = 20.00m;  // stored as percentage, NOT fraction
        const decimal tasaFraction = 0.20m;
        const int days = 30;
        var interes = saldo * (tasaFraction / DaysPerYear) * days;
        var iva = interes * IvaRate;

        var row = MakeRow("Crédito hipotecario", saldoPendiente: saldo,
            intereses: interes, iva: iva, tasa: tasaPercentage, tasaKind: CellKind.Rate);

        var table = MakeSection16Table([row]);
        var ps = PeriodSummaryWithDays(days);
        var model = ModelWith([table], ps);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Tasa as percentage (20.00) normalized to fraction (0.20) → Check 1 reconciles → Pass");
    }

    /// <summary>
    /// Multiple §16 rows — all reconcile (Check 2 exact for both).
    /// </summary>
    [Fact]
    public void Evaluate_Section16MultipleRowsAllPass_ReturnsPass()
    {
        var row1 = MakeRow("Línea 1", saldoPendiente: 3_000m,
            intereses: 300m, iva: 300m * IvaRate, tasa: 0.20m);
        var row2 = MakeRow("Línea 2", saldoPendiente: 5_000m,
            intereses: 750m, iva: 750m * IvaRate, tasa: 0.18m);

        var table = MakeSection16Table([row1, row2]);
        var model = ModelWith([table]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Both rows reconcile on Check 2 → Pass");
    }

    // -----------------------------------------------------------------------
    // (i) Guard 1 — table-level column-count abstain guard (VERIQAN-E2-S3)
    // -----------------------------------------------------------------------

    /// <summary>
    /// §16 table has rows with only 7 value cells (not the expected 9).
    /// Guard 1 must fire and return InsufficientData before any arithmetic is attempted.
    /// Cardinal rule: layout mismatch → abstain (InsufficientData), NEVER Fail.
    /// </summary>
    [Fact]
    public void Evaluate_Section16TableWith7Columns_ReturnsInsufficientData_NotFail()
    {
        // Build a row with 7 value cells (not the 9 required by the §16 column map)
        var sevenCells = new TableCell[]
        {
            TableCell.Amount(0m,       "0.00",   P1()), // [0] Fecha placeholder
            TableCell.Amount(0m,       "0.00",   P1()), // [1] Descripción placeholder
            TableCell.Amount(10_000m,  "10000.00", P1()), // [2] Monto original placeholder
            TableCell.Amount(10_000m,  "10000.00", P1()), // [3] Saldo
            TableCell.Amount(500m,     "500.00",  P1()), // [4] Intereses
            TableCell.Amount(80m,      "80.00",   P1()), // [5] IVA
            TableCell.Amount(200m,     "200.00",  P1()), // [6] Pago requerido
            // [7] and [8] (Núm. de pago, Tasa) intentionally absent — simulates 7-column table
        };
        var row = new TableRow(TableCell.LabelCell("Línea 7col", P1()), sevenCells);

        var table = new FinancialTable(
            sectionNumber: 16,
            sectionName: "Información de otras líneas de crédito",
            status: TableExtractionStatus.Extracted,
            rows: [row],
            locator: P1());

        var model = ModelWith([table]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("result must be success-wrapped");
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "7-column table does not match expected 9-column map → Guard 1 abstains (InsufficientData)");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Cardinal rule: layout mismatch must never produce a false Fail");
        // Message must cite both the observed and the expected column counts
        result.Value.Observed.ShouldNotBeNullOrEmpty();
        result.Value.Observed!.ShouldContain("7");   // observed column count
        result.Value.Observed.ShouldContain("9");     // expected column count
    }

    /// <summary>
    /// §16 table has exactly 9 columns (the expected layout) and IVA reconciles.
    /// Guard 1 must NOT fire — the rule must proceed with normal arithmetic and return Pass.
    /// This verifies that a valid 9-column table is not accidentally abstained.
    /// </summary>
    [Fact]
    public void Evaluate_Section16TableWith9Columns_ProceedsNormally_ReturnsPass()
    {
        const decimal interes = 500.00m;
        var ivaExact = interes * IvaRate; // 80.00

        // MakeRow builds exactly 9 cells — confirms Guard 1 does NOT trigger for the correct layout
        var row = MakeRow("Línea normal 9col",
            saldoPendiente: 10_000m, intereses: interes, iva: ivaExact, tasa: 0.20m);

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Valid 9-column table with correct IVA → Guard 1 does not fire → rule proceeds → Pass");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.InsufficientData,
            "A correctly-shaped 9-column table must not be falsely abstained");
    }

    // -----------------------------------------------------------------------
    // S13 — Configurable IvaRate: rule reads from ILegalToleranceProvider.IvaRate
    // -----------------------------------------------------------------------

    /// <summary>
    /// Proves the §16 rule reads the IVA rate from <see cref="ILegalToleranceProvider.IvaRate"/>
    /// rather than a hard-coded 0.16 constant. When the provider is configured with 0.08 (8%),
    /// a table built with 8%-consistent IVA values must Pass, while the same table would Fail
    /// if the rule used 16% instead (confirming it reads from config).
    /// </summary>
    [Fact]
    public void Evaluate_NonDefaultIvaRate_Check2_UsesConfiguredRateNotHardCodedConstant()
    {
        const decimal configuredIvaRate = 0.08m;  // 8% — deliberately different from 16% default
        const decimal interes = 500.00m;
        // IVA consistent with 8%: 500 × 0.08 = 40.00
        var ivaAt8Pct = interes * configuredIvaRate;

        // Confirm the 8% IVA differs from 16% IVA, so the test is non-vacuous.
        var ivaAt16Pct = interes * 0.16m;
        ivaAt8Pct.ShouldNotBe(ivaAt16Pct,
            "8% and 16% IVA must differ on this input — otherwise the test cannot distinguish the two");

        // Build a §16 row whose IVA cell matches 8% (not 16%).
        var row = MakeRow("Línea 8pct", saldoPendiente: 10_000m,
            intereses: interes, iva: ivaAt8Pct, tasa: 0.20m);
        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx = Ctx(model);

        // Inject a StubToleranceProvider that returns 8% IVA.
        var stubProvider = new StubToleranceProvider(ivaRate: configuredIvaRate);
        var rule = new Section16OtherCreditLinesRule(stubProvider);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        // The rule must Pass: printed IVA matches 8% × Intereses, and the rule is
        // configured for 8%. If the rule hard-coded 0.16, it would compute 80.00 MXN
        // expected but see 40.00 MXN printed — a 40 MXN diff, far beyond the 0.50 MXN
        // tolerance — and would return Fail instead of Pass.
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "IVA at 8% matches the 8%-configured rule — must Pass (proves rule reads from provider)");
    }

    // -----------------------------------------------------------------------
    // Stub: empty tolerance provider
    // -----------------------------------------------------------------------

    private sealed class EmptyToleranceProvider : ILegalToleranceProvider
    {
        public bool Has(string checkId) => false;
        public Tolerance For(string checkId) =>
            throw new InvalidOperationException($"No tolerance registered for '{checkId}'.");
        public decimal IvaRate => 0.16m;
    }

    /// <summary>
    /// Stub provider that exposes the real §16 tolerance and a configurable IVA rate
    /// so tests can prove the rule reads the rate from config, not a hard-coded constant.
    /// </summary>
    private sealed class StubToleranceProvider : ILegalToleranceProvider
    {
        private readonly decimal _ivaRate;
        private static readonly Tolerance CurrencyMxn = new(legalDefault: 0.50m, min: 0.00m, max: 1.00m);

        public StubToleranceProvider(decimal ivaRate) => _ivaRate = ivaRate;

        public bool Has(string checkId) => checkId == "LAW-§16-OTRASLINEAS";
        public Tolerance For(string checkId) =>
            checkId == "LAW-§16-OTRASLINEAS"
                ? CurrencyMxn
                : throw new InvalidOperationException($"No tolerance registered for '{checkId}'.");
        public decimal IvaRate => _ivaRate;
    }
}
