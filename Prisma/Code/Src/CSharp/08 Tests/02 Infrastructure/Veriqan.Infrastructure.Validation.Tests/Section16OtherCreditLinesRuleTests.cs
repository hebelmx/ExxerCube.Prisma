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
/// Unit and integration tests for Story 11.6 §16 other-credit-lines rule
/// (<c>LAW-§16-OTRASLINEAS</c>).
/// </summary>
/// <remarks>
/// <para>
/// §16 is a <b>conditional section</b>: it appears only when the cardholder has other
/// credit lines on the same statement. In ALL current fixtures the section is absent
/// (<see cref="TableExtractionStatus.SectionNotFound"/>), so the primary real-world
/// path is Pass with an N/A note — never Fail.
/// </para>
/// <para>
/// When present (Status == Extracted), the rule verifies per-row IVA arithmetic:
/// <c>|IVA − |Interés| × 0.16| ≤ tolerance</c>.
/// The §16 table structure varies by product; rows without sufficient Amount cells
/// are silently skipped.
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
    /// Creates a §16 <see cref="FinancialTable"/> with the provided rows.
    /// </summary>
    private static FinancialTable MakeSection16Table(IReadOnlyList<TableRow> rows) =>
        new(
            sectionNumber: 16,
            sectionName: "Información de otras líneas de crédito",
            status: TableExtractionStatus.Extracted,
            rows: rows,
            locator: P1());

    /// <summary>
    /// Creates a <see cref="TableRow"/> for §16 with a label and given value cells.
    /// </summary>
    private static TableRow MakeRow(string label, IReadOnlyList<TableCell> valueCells) =>
        new(TableCell.LabelCell(label, P1()), valueCells);

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> with the provided financial tables.
    /// </summary>
    private static StatementModel ModelWith(IReadOnlyList<FinancialTable> tables)
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
            FinancialTables = tables
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
        // StatementModel with a different section only (§20, no §16).
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
    // (b) §16 present with reconciling rows → Pass
    // -----------------------------------------------------------------------

    /// <summary>
    /// §16 present with one row where IVA = Interés × 0.16 exactly.
    /// Rule MUST return Pass.
    /// </summary>
    [Fact]
    public void Evaluate_Section16PresentWithReconcilingRow_ReturnsPass()
    {
        const decimal interes = 500.00m;
        var ivaExpected = interes * IvaRate; // 80.00

        var row = MakeRow("Línea de crédito personal", new List<TableCell>
        {
            TableCell.Amount(interes,     interes.ToString("F2"),     P1()),
            TableCell.Amount(ivaExpected, ivaExpected.ToString("F2"), P1())
        });

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx   = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "IVA exactly matches Interés × 0.16 — should be Pass");
        result.Value.ToleranceApplied.ShouldBe(Tol,
            "ADR-V3: tolerance must be recorded on Pass findings");
    }

    /// <summary>
    /// §16 present; IVA differs from Interés × 0.16 by 0.30 ≤ 0.50 MXN tolerance.
    /// Rule MUST return Pass (within rounding tolerance).
    /// </summary>
    [Fact]
    public void Evaluate_Section16IvaWithinToleranceRounding_ReturnsPass()
    {
        const decimal interes = 500.00m;
        var ivaExact      = interes * IvaRate;  // 80.00
        var ivaReported   = ivaExact + 0.30m;   // 80.30 — within 0.50

        var row = MakeRow("Préstamo personal", new List<TableCell>
        {
            TableCell.Amount(interes,    interes.ToString("F2"),    P1()),
            TableCell.Amount(ivaReported, ivaReported.ToString("F2"), P1())
        });

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx   = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "0.30 MXN difference is within the ±0.50 MXN legal tolerance");
    }

    /// <summary>
    /// Two rows in §16: first reconciles, second reconciles too.
    /// Rule MUST return Pass with a note that mentions "2 row(s) verified".
    /// </summary>
    [Fact]
    public void Evaluate_Section16MultipleReconcilingRows_ReturnsPassWithRowCount()
    {
        const decimal interes1 = 300.00m;
        const decimal interes2 = 750.00m;

        var rows = new List<TableRow>
        {
            MakeRow("Línea 1", new List<TableCell>
            {
                TableCell.Amount(interes1, interes1.ToString("F2"), P1()),
                TableCell.Amount(interes1 * IvaRate, (interes1 * IvaRate).ToString("F2"), P1())
            }),
            MakeRow("Línea 2", new List<TableCell>
            {
                TableCell.Amount(interes2, interes2.ToString("F2"), P1()),
                TableCell.Amount(interes2 * IvaRate, (interes2 * IvaRate).ToString("F2"), P1())
            })
        };

        var table = MakeSection16Table(rows);
        var model = ModelWith([table]);
        var ctx   = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.Observed!.ShouldContain("2 row(s) verified");
    }

    // -----------------------------------------------------------------------
    // (c) §16 present with IVA-vs-interest mismatch beyond tolerance → Fail
    // -----------------------------------------------------------------------

    /// <summary>
    /// IVA reported is 200.00 but Interés × 0.16 = 80.00 (diff = 120.00 >> 0.50).
    /// Rule MUST return Fail citing §16.
    /// </summary>
    [Fact]
    public void Evaluate_Section16IvaMismatchBeyondTolerance_ReturnsFailCitingSection16()
    {
        const decimal interes     = 500.00m;
        const decimal ivaWrong    = 200.00m; // should be 80.00

        var row = MakeRow("Línea de crédito", new List<TableCell>
        {
            TableCell.Amount(interes,  interes.ToString("F2"),  P1()),
            TableCell.Amount(ivaWrong, ivaWrong.ToString("F2"), P1())
        });

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx   = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "IVA 200.00 vs expected 80.00 (diff=120.00) >> 0.50 tolerance → Fail");
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.ToleranceApplied.ShouldBe(Tol);
        result.Value.Observed.ShouldNotBeNullOrEmpty("Fail must carry a reason in Observed");
        result.Value.Observed!.ShouldContain("§16", Case.Sensitive);
    }

    /// <summary>
    /// IVA differs by exactly 0.51 — just beyond the 0.50 MXN legal tolerance → Fail.
    /// </summary>
    [Fact]
    public void Evaluate_Section16IvaJustBeyondTolerance_ReturnsFail()
    {
        const decimal interes  = 500.00m;
        var ivaExact           = interes * IvaRate;   // 80.00
        var ivaReported        = ivaExact + 0.51m;    // 80.51 — just outside tolerance

        var row = MakeRow("Línea personal", new List<TableCell>
        {
            TableCell.Amount(interes,    interes.ToString("F2"),    P1()),
            TableCell.Amount(ivaReported, ivaReported.ToString("F2"), P1())
        });

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx   = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "0.51 MXN difference exceeds the ±0.50 MXN legal tolerance");
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    // -----------------------------------------------------------------------
    // (d) Low-confidence row → InsufficientData (abstain, never Fail)
    // -----------------------------------------------------------------------

    /// <summary>
    /// IVA cell has confidence 0.5 — below the 0.8 legal minimum.
    /// Rule MUST abstain (InsufficientData), never Fail.
    /// </summary>
    [Fact]
    public void Evaluate_Section16LowConfidenceIvaCell_ReturnsInsufficientData()
    {
        const decimal interes = 500.00m;
        var ivaCell = new TableCell(
            "80.00", 80.00m, CellKind.Amount, 0.5, P1()); // below 0.8 threshold

        var row = MakeRow("Línea personal", new List<TableCell>
        {
            TableCell.Amount(interes, interes.ToString("F2"), P1()),
            ivaCell
        });

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx   = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Low-confidence IVA cell must cause abstain, not a Fail verdict");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Cardinal rule: never false-Fail on low-confidence extraction");
    }

    /// <summary>
    /// Interés cell has confidence 0.5 — below threshold.
    /// Rule MUST abstain (InsufficientData).
    /// </summary>
    [Fact]
    public void Evaluate_Section16LowConfidenceInteresCell_ReturnsInsufficientData()
    {
        var interesCell = new TableCell(
            "500.00", 500.00m, CellKind.Amount, 0.5, P1()); // below threshold
        var ivaCell = TableCell.Amount(80.00m, "80.00", P1());

        var row = MakeRow("Línea personal", new List<TableCell>
        {
            interesCell,
            ivaCell
        });

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx   = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Low-confidence Interés cell must cause abstain, not a Fail verdict");
    }

    // -----------------------------------------------------------------------
    // (e) §16 Indeterminate → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section16StatusIndeterminate_ReturnsInsufficientData()
    {
        var table16 = FinancialTable.Indeterminate(
            16, "Información de otras líneas de crédito", P1());

        var model = ModelWith([table16]);
        var ctx   = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Indeterminate table — cannot reliably verify arithmetic → InsufficientData");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Indeterminate must never become a false Fail");
    }

    /// <summary>
    /// §16 status is NoRowsParsed — heading was found but no rows extracted.
    /// Rule MUST return InsufficientData.
    /// </summary>
    [Fact]
    public void Evaluate_Section16StatusNoRowsParsed_ReturnsInsufficientData()
    {
        var table16 = FinancialTable.NoRows(
            16, "Información de otras líneas de crédito", P1());

        var model = ModelWith([table16]);
        var ctx   = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "NoRowsParsed means the table cannot be verified → InsufficientData");
    }

    // -----------------------------------------------------------------------
    // (f) Real-fixture integration: jul_ago §16 is absent → Pass (N/A)
    //     CARDINAL RULE: must never be Fail
    // -----------------------------------------------------------------------

    /// <summary>
    /// Constructs a synthetic model that mirrors the jul_ago fixture state:
    /// §16 is absent (SectionNotFound) because the statement has no other credit lines.
    /// The rule MUST produce Pass with an N/A note — and explicitly MUST NOT be Fail.
    /// </summary>
    [Fact]
    public void Evaluate_JulAgoFixtureShape_Section16Absent_ReturnsPassWithNaNeverFail()
    {
        // The real jul_ago fixture has §16 SectionNotFound — reproduced synthetically
        // so this test is deterministic and independent of a real PDF on disk.
        var table16 = FinancialTable.NotFound(16, "Información de otras líneas de crédito");
        var model = ModelWith([table16]);
        var ctx   = Ctx(model);

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
    // Row-shape defensive: rows with < 2 Amount cells are skipped silently
    // -----------------------------------------------------------------------

    /// <summary>
    /// §16 table is Extracted but every row has only 1 Amount cell (or none).
    /// No row can be IVA-checked → InsufficientData (not Fail).
    /// </summary>
    [Fact]
    public void Evaluate_Section16AllRowsInsufficientShape_ReturnsInsufficientData()
    {
        // A row with only one Amount cell — cannot derive IVA check.
        var row = MakeRow("Línea incompleta", new List<TableCell>
        {
            TableCell.Amount(500.00m, "500.00", P1())   // only 1 Amount cell
        });

        var table = MakeSection16Table([row]);
        var model = ModelWith([table]);
        var ctx   = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "No row with ≥ 2 Amount cells → cannot verify → InsufficientData, not Fail");
    }

    // -----------------------------------------------------------------------
    // Mixed rows: some skipped (NA), one checkable and reconciles → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section16MixedRowsSomeNASomeCheckable_ReturnsPass()
    {
        const decimal interes = 1_000.00m;
        var ivaExact = interes * IvaRate; // 160.00

        var rows = new List<TableRow>
        {
            // Row 1: all NA (bank declared not applicable for this sub-line)
            MakeRow("Sub-línea inactiva", new List<TableCell>
            {
                TableCell.NotApplicableCell("N/A", P1()),
                TableCell.NotApplicableCell("N/A", P1())
            }),
            // Row 2: real data, reconciles exactly
            MakeRow("Sub-línea activa", new List<TableCell>
            {
                TableCell.Amount(interes,  interes.ToString("F2"),  P1()),
                TableCell.Amount(ivaExact, ivaExact.ToString("F2"), P1())
            })
        };

        var table = MakeSection16Table(rows);
        var model = ModelWith([table]);
        var ctx   = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "NA row correctly skipped; checkable row reconciles → Pass");
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    // -----------------------------------------------------------------------
    // IVA cross-check: first row reconciles, second fails → Fail (first failure)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section16SecondRowFailsArithmetic_ReturnsFail()
    {
        const decimal interes1 = 400.00m;
        var iva1 = interes1 * IvaRate; // 64.00 — correct

        const decimal interes2 = 600.00m;
        const decimal iva2Wrong = 200.00m; // should be 96.00

        var rows = new List<TableRow>
        {
            MakeRow("Línea A", new List<TableCell>
            {
                TableCell.Amount(interes1, interes1.ToString("F2"), P1()),
                TableCell.Amount(iva1,     iva1.ToString("F2"),     P1())
            }),
            MakeRow("Línea B", new List<TableCell>
            {
                TableCell.Amount(interes2, interes2.ToString("F2"), P1()),
                TableCell.Amount(iva2Wrong, iva2Wrong.ToString("F2"), P1())
            })
        };

        var table = MakeSection16Table(rows);
        var model = ModelWith([table]);
        var ctx   = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "Second row IVA mismatch (200 vs 96) >> tolerance → Fail");
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed!.ShouldContain("§16", Case.Sensitive);
    }
}
