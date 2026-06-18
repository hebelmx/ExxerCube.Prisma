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
/// Unit and integration tests for the Story 11.3 §19 per-row interest identity rule
/// (<c>LAW-§19-INTERES</c>).
/// </summary>
/// <remarks>
/// <para>
/// All synthetic tests build <see cref="FinancialTable"/> objects with controlled
/// <see cref="TableCell"/> values so every code path is driven deterministically.
/// </para>
/// <para>
/// §19 column semantic order (4 value cells per row):
/// [0] SaldoBase, [1] Núm. de días, [2] Tasa de interés anual, [3] Monto de intereses.
/// </para>
/// <para>
/// Formula verified per row: MontoBankReported ≈ SaldoBase × (TasaFraction / 360) × Días.
/// Rate normalization: raw rate &gt; 1.5 → divide by 100 (percentage number); otherwise already a fraction.
/// </para>
/// <para>
/// §10 cross-check: the "Ordinarios" row rate (normalized to fraction) must equal
/// PeriodSummary.Tasa (stored as fraction) within ±0.0005 fraction-space epsilon.
/// </para>
/// </remarks>
public sealed class Section19InterestRuleTests
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    private const decimal Tol = 0.50m;   // CurrencyToleranceMxn legal default
    private const string ProductId = "TC-S19-TEST";
    private const string CheckId = "LAW-§19-INTERES";

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

    /// <summary>
    /// Creates a §19 table row with the given 4 value cells and a label.
    /// </summary>
    private static TableRow MakeRow(string label, IReadOnlyList<TableCell> valueCells) =>
        new(TableCell.LabelCell(label, P1()), valueCells);

    /// <summary>
    /// Creates a §19 <see cref="FinancialTable"/> with the provided rows.
    /// </summary>
    private static FinancialTable MakeSection19Table(IReadOnlyList<TableRow> rows) =>
        new(
            sectionNumber: 19,
            sectionName: "Saldo sobre el que se calcularon los intereses del periodo",
            status: TableExtractionStatus.Extracted,
            rows: rows,
            locator: P1());

    /// <summary>
    /// Builds a <see cref="StatementModel"/> with the provided §19 table and an optional
    /// <see cref="PeriodSummary"/> (for the §10 cross-check).
    /// </summary>
    private static StatementModel ModelWith(
        FinancialTable table19,
        PeriodSummary? periodSummary = null)
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
            FinancialTables = [table19],
            PeriodSummary = periodSummary
        };
    }

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
        var rules = sp.GetServices<IVecValidationRule>();
        foreach (var r in rules)
            if (r.CheckId == CheckId) return r;
        throw new InvalidOperationException($"Rule {CheckId} not found in DI.");
    }

    // -----------------------------------------------------------------------
    // Helper: build a PeriodSummary with just Tasa set (all others Missing)
    // -----------------------------------------------------------------------

    private static PeriodSummary MakePeriodSummaryWithTasa(ExtractedField<decimal> tasa)
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
            pagoParaNoGenerarIntereses: missingDec,
            pagoMinimo: missingDec,
            pagoMinimoMasMeses: missingDec,
            tasa: tasa,
            cat: missingDec,
            saldoDeudorTotal: missingDec,
            creditoDisponible: missingDec,
            adeudoPeriodoAnterior: null,
            cargosRegularesNoMeses: null,
            cargosComprasAMesesCapital: null,
            montoIntereses: null,
            montoComisiones: null,
            ivaInteresesYComisiones: null,
            pagosYAbonos: null,
            saldoCargosRegulares: null,
            saldoCargosAMeses: null);
    }

    // -----------------------------------------------------------------------
    // Rule metadata contract
    // -----------------------------------------------------------------------

    [Fact]
    public void Rule_Metadata_HasExpectedCheckIdAndClassification()
    {
        var rule = GetRule();
        rule.CheckId.ShouldBe(CheckId);
        rule.DofNumeral.ShouldBe("Acuerdo §19");
        rule.Classification.ShouldBe(RuleClassification.TenantTightenableOnly);
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }

    // -----------------------------------------------------------------------
    // (a) Row that recomputes correctly → Pass
    // -----------------------------------------------------------------------

    /// <summary>
    /// SaldoBase=10000, Días=30, Tasa=0.2736 (fraction form).
    /// Expected = 10000 × (0.2736/360) × 30 = 10000 × 0.00076 × 30 = 228.00.
    /// Reported = 228.00 → diff = 0 → Pass.
    /// </summary>
    [Fact]
    public void Evaluate_SingleRowRecomputesExactly_ReturnsPass()
    {
        const decimal saldo = 10_000m;
        const decimal dias = 30m;
        const decimal tasaFraction = 0.2736m;
        var expected = saldo * (tasaFraction / 360m) * dias;  // 228.00

        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            TableCell.Rate(tasaFraction, "27.36%", P1()),
            TableCell.Amount(expected, expected.ToString("F2"), P1())
        });

        var table = MakeSection19Table([row]);
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(CheckId);
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    /// <summary>
    /// Same as above but with rounding: reported monto differs by 0.30 ≤ 0.50 → Pass.
    /// </summary>
    [Fact]
    public void Evaluate_RowWithinToleranceRounding_ReturnsPass()
    {
        const decimal saldo = 10_000m;
        const decimal dias = 30m;
        const decimal tasaFraction = 0.2736m;
        var exactExpected = saldo * (tasaFraction / 360m) * dias;
        var reportedMonto = exactExpected + 0.30m; // within 0.50 tolerance

        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            TableCell.Rate(tasaFraction, "27.36%", P1()),
            TableCell.Amount(reportedMonto, reportedMonto.ToString("F2"), P1())
        });

        var table = MakeSection19Table([row]);
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "0.30 MXN difference is within the ±0.50 MXN legal tolerance");
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    // -----------------------------------------------------------------------
    // (b) Row whose monto mismatches beyond tolerance → Fail citing §19
    // -----------------------------------------------------------------------

    /// <summary>
    /// SaldoBase=10000, Días=30, Tasa=0.2736, expectedMonto≈228.00.
    /// Reported monto=300.00 → diff=72.00 >> 0.50 → Fail.
    /// </summary>
    [Fact]
    public void Evaluate_RowMontoBeyondTolerance_ReturnsFail()
    {
        const decimal saldo = 10_000m;
        const decimal dias = 30m;
        const decimal tasaFraction = 0.2736m;
        const decimal wrongMonto = 300.00m; // way off

        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            TableCell.Rate(tasaFraction, "27.36%", P1()),
            TableCell.Amount(wrongMonto, wrongMonto.ToString("F2"), P1())
        });

        var table = MakeSection19Table([row]);
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.ToleranceApplied.ShouldBe(Tol);
        result.Value.Observed.ShouldNotBeNullOrEmpty("Fail must carry a reason in Observed");
        result.Value.Observed.ShouldContain("§19", Case.Sensitive);
    }

    /// <summary>
    /// Monto differs by exactly 0.51 — just beyond the 0.50 legal tolerance → Fail.
    /// </summary>
    [Fact]
    public void Evaluate_RowJustBeyondTolerance_ReturnsFail()
    {
        const decimal saldo = 10_000m;
        const decimal dias = 30m;
        const decimal tasaFraction = 0.2736m;
        var exactExpected = saldo * (tasaFraction / 360m) * dias;
        var reportedMonto = exactExpected + 0.51m; // just outside tolerance

        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            TableCell.Rate(tasaFraction, "27.36%", P1()),
            TableCell.Amount(reportedMonto, reportedMonto.ToString("F2"), P1())
        });

        var table = MakeSection19Table([row]);
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "0.51 MXN difference exceeds the ±0.50 MXN legal tolerance");
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    // -----------------------------------------------------------------------
    // (c) All-NA rows → InsufficientData (the real-fixture case)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_AllRowsAreNA_ReturnsInsufficientData()
    {
        // Build 6 rows where every value cell is NotApplicable (typical for jul_ago fixture)
        var rows = new List<TableRow>();
        var labels = new[]
        {
            "Ordinarios",
            "Moratorio",
            "De saldo revolvente a tasa preferencial",
            "De compras y cargos diferidos a meses con intereses",
            "Por disposiciones de efectivo",
            "Por disposiciones de efectivo de otras líneas de crédito"
        };
        foreach (var label in labels)
        {
            rows.Add(MakeRow(label, new List<TableCell>
            {
                TableCell.NotApplicableCell("NA", P1()),
                TableCell.NotApplicableCell("NA", P1()),
                TableCell.NotApplicableCell("NA", P1()),
                TableCell.NotApplicableCell("NA", P1())
            }));
        }

        var table = MakeSection19Table(rows);
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(CheckId);
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed.ShouldNotBeNullOrEmpty(
            "InsufficientData must carry a reason in Observed");
    }

    // -----------------------------------------------------------------------
    // (d) Low-confidence cell → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_RowWithLowConfidenceCell_ReturnsInsufficientData()
    {
        // Monto cell has confidence 0.5 — below the 0.8 legal threshold
        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(10_000m, "10000.00", P1()),
            TableCell.Days(30m, "30", P1()),
            TableCell.Rate(0.2736m, "27.36%", P1()),
            new TableCell("228.00", 228m, CellKind.Amount, 0.5, P1())   // low confidence
        });

        var table = MakeSection19Table([row]);
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "A low-confidence cell must cause abstain, not a verdict");
    }

    // -----------------------------------------------------------------------
    // (e) §19 Ordinarios rate ≠ §10 tasa → Fail
    // -----------------------------------------------------------------------

    /// <summary>
    /// Row recomputes correctly (so that check passes), but the Ordinarios rate (0.2736)
    /// differs from the §10 PeriodSummary.Tasa (0.1800) by 0.0936 >> 0.0005 epsilon → Fail.
    /// </summary>
    [Fact]
    public void Evaluate_OrdinariosRateNotEqualSection10Tasa_ReturnsFail()
    {
        const decimal saldo = 10_000m;
        const decimal dias = 30m;
        const decimal ordinariosTasa = 0.2736m; // 27.36%
        var correctMonto = saldo * (ordinariosTasa / 360m) * dias;

        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            TableCell.Rate(ordinariosTasa, "27.36%", P1()),
            TableCell.Amount(correctMonto, correctMonto.ToString("F2"), P1())
        });

        // §10 tasa is different (18.00% as fraction = 0.1800)
        const decimal section10Tasa = 0.1800m;
        var periodSummary = MakePeriodSummaryWithTasa(
            ExtractedField<decimal>.Found(section10Tasa, P1()));

        var table = MakeSection19Table([row]);
        var model = ModelWith(table, periodSummary);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "Ordinarios rate (0.2736) ≠ §10 tasa (0.1800) by 0.0936 >> 0.0005 epsilon");
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed.ShouldNotBeNullOrEmpty("Fail must carry a reason");
    }

    /// <summary>
    /// Ordinarios rate and §10 tasa differ by exactly 0.0004 ≤ 0.0005 epsilon → Pass.
    /// </summary>
    [Fact]
    public void Evaluate_OrdinariosRateWithinSection10Epsilon_ReturnsPass()
    {
        const decimal saldo = 10_000m;
        const decimal dias = 30m;
        const decimal ordinariosTasa = 0.2736m;
        var correctMonto = saldo * (ordinariosTasa / 360m) * dias;

        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            TableCell.Rate(ordinariosTasa, "27.36%", P1()),
            TableCell.Amount(correctMonto, correctMonto.ToString("F2"), P1())
        });

        // §10 tasa differs by 0.0004 (within 0.0005 epsilon)
        const decimal section10Tasa = 0.2736m + 0.0004m;
        var periodSummary = MakePeriodSummaryWithTasa(
            ExtractedField<decimal>.Found(section10Tasa, P1()));

        var table = MakeSection19Table([row]);
        var model = ModelWith(table, periodSummary);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "0.0004 fraction difference is within the ±0.0005 rate epsilon");
    }

    // -----------------------------------------------------------------------
    // (f) Percentage-vs-fraction scale normalization
    // -----------------------------------------------------------------------

    /// <summary>
    /// Rate cell carries 27.36 (percentage form, > 1.5) — the rule must divide by 100
    /// before applying the formula. Computed monto must match what the bank would compute
    /// from 27.36/100 = 0.2736.
    /// </summary>
    [Fact]
    public void Evaluate_RateCellAsPercentageNumber_NormalizesCorrectly_ReturnsPass()
    {
        const decimal saldo = 10_000m;
        const decimal dias = 30m;
        const decimal rateAsPercentage = 27.36m;  // stored as percentage number (> 1.5)
        const decimal rateFraction = rateAsPercentage / 100m;  // 0.2736
        var expected = saldo * (rateFraction / 360m) * dias;

        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            // ParsedValue = 27.36 (> 1.5 → rule must normalize to 0.2736)
            new TableCell("27.36", rateAsPercentage, CellKind.Rate, 1.0, P1()),
            TableCell.Amount(expected, expected.ToString("F2"), P1())
        });

        var table = MakeSection19Table([row]);
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Rule must normalize percentage-form rate (27.36) to fraction (0.2736) before formula");
    }

    /// <summary>
    /// Rate cell carries 0.2736 (fraction form, ≤ 1.5) — the rule uses it directly as a fraction.
    /// </summary>
    [Fact]
    public void Evaluate_RateCellAsFraction_UsesDirectly_ReturnsPass()
    {
        const decimal saldo = 10_000m;
        const decimal dias = 30m;
        const decimal rateFraction = 0.2736m;  // stored as fraction (≤ 1.5)
        var expected = saldo * (rateFraction / 360m) * dias;

        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            TableCell.Rate(rateFraction, "0.2736", P1()),
            TableCell.Amount(expected, expected.ToString("F2"), P1())
        });

        var table = MakeSection19Table([row]);
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Rule must use fraction-form rate (0.2736) directly without dividing by 100");
    }

    // -----------------------------------------------------------------------
    // (g) §19 table absent → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section19Absent_ReturnsInsufficientData()
    {
        // Model has no §19 table at all (just an unrelated §20 placeholder)
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
            FinancialTables = [FinancialTable.NotFound(20, "Distribución de tu último pago")]
        };
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed.ShouldNotBeNullOrEmpty("InsufficientData must carry a reason");
    }

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var ctx = Ctx(model: null);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_Section19StatusSectionNotFound_ReturnsInsufficientData()
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
            FinancialTables = [FinancialTable.NotFound(19,
                "Saldo sobre el que se calcularon los intereses del periodo")]
        };
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_Section19StatusIndeterminate_ReturnsInsufficientData()
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
            FinancialTables = [FinancialTable.Indeterminate(19,
                "Saldo sobre el que se calcularon los intereses del periodo", P1())]
        };
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
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
    // Mixed rows: some NA, one checkable → Pass on the checkable row
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_MixedRowsSomeNASomeCheckable_ReturnsPassForCheckableRow()
    {
        const decimal saldo = 15_000m;
        const decimal dias = 31m;
        const decimal tasaFraction = 0.2500m;
        var correctMonto = saldo * (tasaFraction / 360m) * dias;

        var rows = new List<TableRow>
        {
            // Ordinarios — real data, recomputes correctly
            MakeRow("Ordinarios", new List<TableCell>
            {
                TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
                TableCell.Days(dias, dias.ToString(), P1()),
                TableCell.Rate(tasaFraction, "25.00%", P1()),
                TableCell.Amount(correctMonto, correctMonto.ToString("F2"), P1())
            }),
            // Moratorio — all NA
            MakeRow("Moratorio", new List<TableCell>
            {
                TableCell.NotApplicableCell("NA", P1()),
                TableCell.NotApplicableCell("NA", P1()),
                TableCell.NotApplicableCell("NA", P1()),
                TableCell.NotApplicableCell("NA", P1())
            }),
            // De saldo revolvente — all NA
            MakeRow("De saldo revolvente a tasa preferencial", new List<TableCell>
            {
                TableCell.NotApplicableCell("NA", P1()),
                TableCell.NotApplicableCell("NA", P1()),
                TableCell.NotApplicableCell("NA", P1()),
                TableCell.NotApplicableCell("NA", P1())
            })
        };

        var table = MakeSection19Table(rows);
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "The one checkable row recomputes correctly; NA rows are correctly skipped");
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    // -----------------------------------------------------------------------
    // §10 cross-check: Ordinarios tasa NA or PeriodSummary absent → skip cross-check
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_OrdinariosTasaIsNA_SkipsCrossCheck_ReturnsPass()
    {
        const decimal saldo = 10_000m;
        const decimal dias = 30m;
        // Tasa cell is NA for Ordinarios — cross-check must be skipped
        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            TableCell.NotApplicableCell("NA", P1()),   // tasa is NA
            TableCell.NotApplicableCell("NA", P1())    // monto is NA — row will be fully skipped
        });

        // But another row (Moratorio) has a good recompute
        const decimal saldoM = 5_000m;
        const decimal diasM = 30m;
        const decimal tasaM = 0.3000m;
        var montoM = saldoM * (tasaM / 360m) * diasM;
        var rowM = MakeRow("Moratorio", new List<TableCell>
        {
            TableCell.Amount(saldoM, saldoM.ToString("F2"), P1()),
            TableCell.Days(diasM, diasM.ToString(), P1()),
            TableCell.Rate(tasaM, "30.00%", P1()),
            TableCell.Amount(montoM, montoM.ToString("F2"), P1())
        });

        var table = MakeSection19Table(new List<TableRow> { row, rowM });
        // Provide a PeriodSummary.Tasa that would differ — but cross-check skipped since Ordinarios row is NA
        var periodSummary = MakePeriodSummaryWithTasa(
            ExtractedField<decimal>.Found(0.1000m, P1())); // very different, but Ordinarios tasa is NA
        var model = ModelWith(table, periodSummary);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Cross-check skipped when Ordinarios row has NA tasa; Moratorio row recomputes correctly");
    }

    [Fact]
    public void Evaluate_NoPeriodSummary_SkipsCrossCheck_ReturnsPass()
    {
        const decimal saldo = 10_000m;
        const decimal dias = 30m;
        const decimal tasaFraction = 0.2736m;
        var correctMonto = saldo * (tasaFraction / 360m) * dias;

        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            TableCell.Rate(tasaFraction, "27.36%", P1()),
            TableCell.Amount(correctMonto, correctMonto.ToString("F2"), P1())
        });

        var table = MakeSection19Table([row]);
        var model = ModelWith(table, periodSummary: null); // no PeriodSummary
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Cross-check skipped when PeriodSummary is null; row still recomputes correctly");
    }

    // -----------------------------------------------------------------------
    // ToleranceApplied contract (ADR-V3)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_PassVerdict_RecordsToleranceApplied()
    {
        const decimal saldo = 5_000m;
        const decimal dias = 31m;
        const decimal tasaFraction = 0.2200m;
        var correctMonto = saldo * (tasaFraction / 360m) * dias;

        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            TableCell.Rate(tasaFraction, "22.00%", P1()),
            TableCell.Amount(correctMonto, correctMonto.ToString("F2"), P1())
        });

        var table = MakeSection19Table([row]);
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(Tol,
            "ADR-V3: the applied tolerance must be recorded on Pass findings");
    }

    // -----------------------------------------------------------------------
    // (h) Scale detection from RawText '%' sign
    // -----------------------------------------------------------------------

    /// <summary>
    /// Tasa cell has Kind=Amount (extractor did not tag it as Rate), ParsedValue=1.2,
    /// and RawText="1.2%" — the '%' sign explicitly indicates a percentage number.
    /// Rule must divide by 100 (→ 0.012) before formula.
    /// <para>
    /// Note: <see cref="CellKind.Rate"/> cells carry the value already normalised to a
    /// decimal fraction (domain contract), so this '%'-detection path only fires for
    /// non-Rate cells whose extractor did not normalise the value.
    /// </para>
    /// </summary>
    [Fact]
    public void Evaluate_NonRateCellWith1Point2PercentInRawText_TreatedAsPercentage_ReturnsPass()
    {
        const decimal saldo = 10_000m;
        const decimal dias = 30m;
        // Extractor left the value as the percentage number 1.2 (not yet ÷100) in an Amount cell.
        const decimal rateAsStoredValue = 1.2m;
        const decimal rateFractionExpected = rateAsStoredValue / 100m; // 0.012
        var correctMonto = saldo * (rateFractionExpected / 360m) * dias;

        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            // Kind=Amount (not Rate) + RawText '%' → must be treated as percentage number (÷100)
            new TableCell("1.2%", rateAsStoredValue, CellKind.Amount, 1.0, P1()),
            TableCell.Amount(correctMonto, correctMonto.ToString("F2"), P1())
        });

        var table = MakeSection19Table([row]);
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Non-Rate cell with RawText '1.2%' signals a percentage — must ÷100 before formula");
    }

    /// <summary>
    /// Tasa cell has Kind=Amount (extractor did not tag it as Rate), ParsedValue=1.2,
    /// no '%' in RawText. Value is in ambiguous zone (1.0, 1.5] — the rule cannot
    /// determine scale without an explicit '%' signal. Must return InsufficientData.
    /// <para>
    /// Note: <see cref="CellKind.Rate"/> cells are always fractions (domain contract)
    /// and are never ambiguous. This test exercises the non-Rate fallback path.
    /// </para>
    /// </summary>
    [Fact]
    public void Evaluate_NonRateCellAmbiguousScale_ReturnsInsufficientData()
    {
        const decimal saldo = 10_000m;
        const decimal dias = 30m;
        const decimal ambiguousRate = 1.2m; // in (1.0, 1.5] with no '%' → ambiguous

        // Monto set to the "treat as fraction" value to avoid confounding with a Fail.
        var montoIfFraction = saldo * (ambiguousRate / 360m) * dias;

        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            // Kind=Amount (not Rate) + no '%' in RawText + value 1.2 in ambiguous zone
            new TableCell("1.2", ambiguousRate, CellKind.Amount, 1.0, P1()),
            TableCell.Amount(montoIfFraction, montoIfFraction.ToString("F2"), P1())
        });

        var table = MakeSection19Table([row]);
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Non-Rate cell with value 1.2 and no '%' is ambiguous — must abstain rather than guess scale");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Cardinal rule: never false-Fail on ambiguous extraction");
    }

    // -----------------------------------------------------------------------
    // (i) Unregistered tolerance → InsufficientData (not exception)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_UnregisteredTolerance_ReturnsInsufficientData_NotException()
    {
        var rule = new Section19InterestPerRowRule(new EmptyToleranceProvider());

        const decimal saldo = 10_000m;
        const decimal dias = 30m;
        const decimal tasaFraction = 0.2736m;
        var correctMonto = saldo * (tasaFraction / 360m) * dias;

        var row = MakeRow("Ordinarios", new List<TableCell>
        {
            TableCell.Amount(saldo, saldo.ToString("F2"), P1()),
            TableCell.Days(dias, dias.ToString(), P1()),
            TableCell.Rate(tasaFraction, "27.36%", P1()),
            TableCell.Amount(correctMonto, correctMonto.ToString("F2"), P1())
        });
        var table = MakeSection19Table([row]);
        var model = ModelWith(table);
        var ctx = Ctx(model);

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
    // Real-fixture integration test: jul_ago §19 is mostly NA → InsufficientData
    // (never a false Fail)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Constructs a synthetic §19 model that mirrors the jul_ago fixture's known state:
    /// all 6 rows are NA (confirmed by the Story 11.1 extraction spike).
    /// The rule MUST produce InsufficientData — never a false Fail.
    /// </summary>
    [Fact]
    public void Evaluate_JulAgoFixtureShape_AllNaRows_ProducesInsufficientDataNeverFail()
    {
        // The real jul_ago §19 table has 6 rows, all cells NA.
        // We reproduce the shape synthetically so this test is deterministic and
        // does not depend on a real PDF being available in the test host.
        var labels = new[]
        {
            "Ordinarios",
            "Moratorio",
            "De saldo revolvente a tasa preferencial",
            "De compras y cargos diferidos a meses con intereses",
            "Por disposiciones de efectivo",
            "Por disposiciones de efectivo de otras líneas de crédito"
        };

        var rows = new List<TableRow>();
        foreach (var label in labels)
        {
            rows.Add(MakeRow(label, new List<TableCell>
            {
                TableCell.NotApplicableCell("NA", P1()),
                TableCell.NotApplicableCell("NA", P1()),
                TableCell.NotApplicableCell("NA", P1()),
                TableCell.NotApplicableCell("NA", P1())
            }));
        }

        var table = MakeSection19Table(rows);
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(CheckId);

        // The cardinal rule: NEVER a false Fail on a real statement.
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "A billing-run gate must NEVER false-Fail on a statement where §19 is all-NA");

        // Correct verdict for the all-NA case is InsufficientData.
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "All-NA rows → no recomputable data → InsufficientData is the correct abstain verdict");
    }
}
