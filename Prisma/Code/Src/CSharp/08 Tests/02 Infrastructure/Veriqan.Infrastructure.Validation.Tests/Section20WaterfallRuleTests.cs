using System;
using System.Collections.Generic;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit tests for the Story 11.2 §20 "Distribución de tu último pago" waterfall identity rule
/// (<c>LAW-§20-WATERFALL</c>).
/// </summary>
/// <remarks>
/// <para>
/// All synthetic tests build <see cref="FinancialTable"/> objects with controlled
/// <see cref="TableCell"/> values so every code path can be driven deterministically.
/// </para>
/// <para>
/// The identity verified: <c>|Pagos y abonos| ≈ Regulares + AMesesSIN + AMesesCON + Intereses + IVA − SaldoAFavor</c>.
/// </para>
/// <para>
/// Calibration note (from Story 11.1 extraction spike): the real <c>01+Dummie+VEC+jul_ago+20252.pdf</c>
/// fixture is internally inconsistent — components sum to 61,033.35 but pagos y abonos is 67,796.35
/// (Δ 6,763.00 >> tolerance) — so that fixture MUST NOT produce a Pass verdict.
/// The integration test at the bottom of this file asserts Fail or InsufficientData.
/// </para>
/// </remarks>
public sealed class Section20WaterfallRuleTests
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    private const decimal Tol = 0.50m;   // CurrencyToleranceMxn legal default
    private const string ProductId = "TC-S20-TEST";
    private const string CheckId = "LAW-§20-WATERFALL";

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
    /// Creates a <see cref="TableCell"/> for a successfully-parsed currency amount.
    /// Confidence = 1.0, Kind = Amount.
    /// </summary>
    private static TableCell AmountCell(decimal value) =>
        TableCell.Amount(value, value.ToString("F2"), P1());

    /// <summary>
    /// Creates a §20 <see cref="FinancialTable"/> with a single row containing exactly the
    /// provided 7 value cells.
    /// </summary>
    private static FinancialTable MakeSection20Table(IReadOnlyList<TableCell> valueCells) =>
        new(
            sectionNumber: 20,
            sectionName: "Distribución de tu último pago",
            status: TableExtractionStatus.Extracted,
            rows: [new TableRow(TableCell.LabelCell("Distribución", P1()), valueCells)],
            locator: P1());

    /// <summary>
    /// Builds a <see cref="StatementModel"/> (header fields all Missing) with the supplied
    /// <see cref="FinancialTable"/> entries in <c>FinancialTables</c>.
    /// </summary>
    private static StatementModel ModelWith(IReadOnlyList<FinancialTable> tables)
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
            FinancialTables = tables
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
    // Core metadata contract
    // -----------------------------------------------------------------------

    [Fact]
    public void Rule_Metadata_HasExpectedCheckIdAndClassification()
    {
        var rule = GetRule();
        rule.CheckId.ShouldBe(CheckId);
        rule.DofNumeral.ShouldBe("Acuerdo §20");
        rule.Classification.ShouldBe(RuleClassification.TenantTightenableOnly);
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }

    // -----------------------------------------------------------------------
    // InsufficientData paths — abstain logic
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var ctx = Ctx(model: null);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(CheckId);
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_Section20Absent_ReturnsInsufficientData()
    {
        var rule = GetRule();
        // Build a model with NO §20 table (only a §8 placeholder)
        var table8 = FinancialTable.NotFound(8, "Indicadores del Costo Anual Total");
        var model = ModelWith([table8]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed.ShouldNotBeNull("InsufficientData must carry a reason in Observed");
    }

    [Fact]
    public void Evaluate_Section20StatusSectionNotFound_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var table20 = FinancialTable.NotFound(20, "Distribución de tu último pago");
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_Section20StatusIndeterminate_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var table20 = FinancialTable.Indeterminate(20, "Distribución de tu último pago", P1());
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_Section20StatusNoRowsParsed_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var table20 = FinancialTable.NoRows(20, "Distribución de tu último pago", P1());
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_RowHasFewerThan7Cells_ReturnsInsufficientData()
    {
        var rule = GetRule();
        // Only 6 cells — one short
        var cells = new List<TableCell>
        {
            AmountCell(67796.35m),
            AmountCell(0m),
            AmountCell(61033.35m),
            AmountCell(0m),
            AmountCell(0m),
            AmountCell(0m)
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_CellWithNoParsedValue_ReturnsInsufficientData()
    {
        var rule = GetRule();
        // col[2] has a parse failure (no ParsedValue)
        var cells = new List<TableCell>
        {
            AmountCell(-67796.35m),
            AmountCell(0m),
            TableCell.ParseFailure("???", P1()),  // col[2] — parsed value is null
            AmountCell(61033.35m),
            AmountCell(0m),
            AmountCell(0m),
            AmountCell(0m)
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_CellWithLowConfidence_ReturnsInsufficientData()
    {
        var rule = GetRule();
        // col[4] has confidence 0.5 — below the 0.8 default threshold
        var cells = new List<TableCell>
        {
            AmountCell(-67796.35m),
            AmountCell(0m),
            AmountCell(61033.35m),
            AmountCell(0m),
            new TableCell("500.00", 500m, CellKind.Amount, 0.5, P1()), // low confidence
            AmountCell(0m),
            AmountCell(0m)
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_CellWithWrongKind_ReturnsInsufficientData()
    {
        var rule = GetRule();
        // col[6] is a Rate cell instead of Amount
        var cells = new List<TableCell>
        {
            AmountCell(-67796.35m),
            AmountCell(0m),
            AmountCell(61033.35m),
            AmountCell(0m),
            AmountCell(0m),
            AmountCell(0m),
            TableCell.Rate(0.16m, "16%", P1())  // col[6] — wrong kind
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_CancellationRequested_ReturnsCancelled()
    {
        var rule = GetRule();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ctx = Ctx(model: null);

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsSuccess.ShouldBeFalse("Cancelled result must be a failure Result");
    }

    // -----------------------------------------------------------------------
    // Happy-path: synthetic reconciling case → Pass
    // -----------------------------------------------------------------------

    /// <summary>
    /// Synthetic case where the 7-column identity holds exactly.
    /// pagos = 10_000, components = 6000 + 2000 + 1000 + 500 + 750 − 250 = 10_000 → diff = 0.
    /// </summary>
    [Fact]
    public void Evaluate_PerfectReconciliation_ReturnsPass()
    {
        var rule = GetRule();
        //
        // col[0] pagos y abonos  (signed negative, abs = 10,000)
        // col[1] regulares                                6,000
        // col[2] a meses SIN intereses                   2,000
        // col[3] a meses CON intereses                   1,000
        // col[4] intereses y comisiones                    500
        // col[5] IVA                                       750
        // col[6] saldo a favor                             250
        // components = 6000+2000+1000+500+750-250 = 10,000 → Δ=0 → Pass
        var cells = new List<TableCell>
        {
            AmountCell(-10_000.00m),
            AmountCell(6_000.00m),
            AmountCell(2_000.00m),
            AmountCell(1_000.00m),
            AmountCell(500.00m),
            AmountCell(750.00m),
            AmountCell(250.00m)
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(CheckId);
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(Tol);
        result.Value.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass);
    }

    /// <summary>
    /// Synthetic within-tolerance rounding case: difference is 0.30 MXN (≤ 0.50 MXN legal floor) → Pass.
    /// </summary>
    [Fact]
    public void Evaluate_WithinToleranceRounding_ReturnsPass()
    {
        var rule = GetRule();
        // pagos = 10,000.00; components = 6000+2000+1000+500+750.30-250 = 10,000.30 → Δ=0.30 ≤ 0.50
        var cells = new List<TableCell>
        {
            AmountCell(-10_000.00m),
            AmountCell(6_000.00m),
            AmountCell(2_000.00m),
            AmountCell(1_000.00m),
            AmountCell(500.00m),
            AmountCell(750.30m),
            AmountCell(250.00m)
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "0.30 MXN difference is within the ±0.50 MXN legal tolerance");
        result.Value.ToleranceApplied.ShouldBe(Tol);
    }

    // -----------------------------------------------------------------------
    // Fail path: synthetic mismatch beyond tolerance → Fail
    // -----------------------------------------------------------------------

    /// <summary>
    /// Synthetic case where the identity fails by 6,763.00 MXN (well beyond 0.50 MXN).
    /// This mirrors the calibration finding from the real jul_ago fixture.
    /// </summary>
    [Fact]
    public void Evaluate_LargeMismatch_ReturnsFail()
    {
        var rule = GetRule();
        // pagos = 67,796.35; components = 61,033.35+0+0+0+0-0 = 61,033.35 → Δ=6,763.00 >> 0.50
        var cells = new List<TableCell>
        {
            AmountCell(-67_796.35m),  // pagos y abonos
            AmountCell(61_033.35m),   // regulares
            AmountCell(0m),
            AmountCell(0m),
            AmountCell(0m),
            AmountCell(0m),
            AmountCell(0m)
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(CheckId);
        result.Value.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.ToleranceApplied.ShouldBe(Tol);
        result.Value.Expected.ShouldNotBeNullOrEmpty("Fail finding must carry expected (components sum)");
        result.Value.Observed.ShouldNotBeNullOrEmpty("Fail finding must carry observed (pagos y abonos)");
    }

    [Fact]
    public void Evaluate_JustBeyondTolerance_ReturnsFail()
    {
        var rule = GetRule();
        // pagos = 10,000.00; components = 6000+2000+1000+500+751.00-250 = 10,001.00 → Δ=1.00 > 0.50
        var cells = new List<TableCell>
        {
            AmountCell(-10_000.00m),
            AmountCell(6_000.00m),
            AmountCell(2_000.00m),
            AmountCell(1_000.00m),
            AmountCell(500.00m),
            AmountCell(751.00m),
            AmountCell(250.00m)
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "1.00 MXN difference exceeds the ±0.50 MXN legal tolerance");
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    // -----------------------------------------------------------------------
    // Dual-verdict: tenant stricter than legal floor
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the absolute difference is 0.30 (within legal 0.50) but the tenant sets 0.20,
    /// the effective verdict is Fail while LegalBaselineVerdict is Pass.
    /// </summary>
    [Fact]
    public void Evaluate_TenantStricterThanLegal_DualVerdictDivergence()
    {
        var rule = GetRule();

        // diff = 0.30 — within legal (0.50) but beyond tenant (0.20)
        var cells = new List<TableCell>
        {
            AmountCell(-10_000.00m),
            AmountCell(6_000.00m),
            AmountCell(2_000.00m),
            AmountCell(1_000.00m),
            AmountCell(500.00m),
            AmountCell(750.30m),
            AmountCell(250.00m)
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);

        // Build a ResolvedTenantProfile that tightens this rule to 0.20 MXN
        var tenantProfile = new ResolvedTenantProfile(
            tenantId: "TENANT-STRICT",
            tenantName: "Strict Test Tenant",
            effectiveTolerances: new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                [CheckId] = 0.20m
            },
            deviations: []);

        var ctx = new VerificationContext(
            bundle: BundleWithTolerance(),
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(BundleWithTolerance()),
            priorStatement: null,
            toleranceConfig: BundleWithTolerance().ToleranceConfig,
            statementModel: model,
            tenantProfile: tenantProfile);

        var result = rule!.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "Effective verdict should Fail under the stricter 0.20 tenant tolerance");
        result.Value.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass,
            "Legal baseline should Pass since 0.30 ≤ 0.50 legal floor");
        result.Value.ToleranceApplied.ShouldBe(0.20m);
    }

    // -----------------------------------------------------------------------
    // Sign handling: pagos y abonos may be positive or negative — abs is used
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_PagosYAbonosPositiveSigned_StillPasses()
    {
        var rule = GetRule();
        // Same identity as the perfect-reconciliation case but col[0] is +10,000 not −10,000
        var cells = new List<TableCell>
        {
            AmountCell(10_000.00m),   // positive (bank prints it without sign)
            AmountCell(6_000.00m),
            AmountCell(2_000.00m),
            AmountCell(1_000.00m),
            AmountCell(500.00m),
            AmountCell(750.00m),
            AmountCell(250.00m)
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Rule must use Math.Abs(pagos) — sign should not affect verdict");
    }

    // -----------------------------------------------------------------------
    // ToleranceApplied contract (ADR-V3)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Pass_RecordsToleranceApplied()
    {
        var rule = GetRule();
        // Exact reconciliation
        var cells = new List<TableCell>
        {
            AmountCell(-5_000.00m),
            AmountCell(3_000.00m),
            AmountCell(1_000.00m),
            AmountCell(500.00m),
            AmountCell(300.00m),
            AmountCell(200.00m),
            AmountCell(0m)
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(Tol,
            "ADR-V3: the applied tolerance must be recorded on Pass findings");
    }

    // -----------------------------------------------------------------------
    // Sign-convention guard (VERIQAN-E2-S2): col[6] saldo-a-favor may be
    // printed as a NEGATIVE amount by some banks.  The rule must normalise to
    // abs(value) before subtracting so a negative print does not inflate the
    // components sum and produce a false-Fail.
    // -----------------------------------------------------------------------

    /// <summary>
    /// When col[6] (saldo a favor) is printed as a negative amount AND the
    /// identity holds once normalised, the rule must return Pass — not Fail.
    /// This is the false-Fail regression guard for VERIQAN-E2-S2.
    /// </summary>
    [Fact]
    public void Evaluate_SaldoAFavorNegativeSigned_IdentityHoldsAfterNormalisation_ReturnsPass()
    {
        var rule = GetRule();
        //
        // col[0] pagos y abonos (signed negative, abs = 10,000)  -10,000
        // col[1] regulares                                          6,000
        // col[2] a meses SIN intereses                             2,000
        // col[3] a meses CON intereses                             1,000
        // col[4] intereses y comisiones                              500
        // col[5] IVA                                                 750
        // col[6] saldo a favor — bank prints as NEGATIVE:           -250
        //
        // Without the fix: components = 6000+2000+1000+500+750 - (-250) = 10,250 → Δ=250 → false-Fail
        // With the fix:    balanceCredit = abs(-250) = 250
        //                  components = 6000+2000+1000+500+750 - 250 = 10,000 → Δ=0 → Pass
        var cells = new List<TableCell>
        {
            AmountCell(-10_000.00m),
            AmountCell(6_000.00m),
            AmountCell(2_000.00m),
            AmountCell(1_000.00m),
            AmountCell(500.00m),
            AmountCell(750.00m),
            AmountCell(-250.00m)   // negative: bank prints saldo-a-favor negated
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Negative saldo-a-favor must be normalised to abs(value) before subtraction; " +
            "identity holds → must Pass, not false-Fail.");
    }

    /// <summary>
    /// When col[6] saldo-a-favor is negative AND the normalised interpretation does NOT
    /// satisfy the tolerance, but the raw (un-normalised) interpretation would, the rule
    /// is in an ambiguous zone and must abstain (InsufficientData) rather than emit Fail.
    /// </summary>
    [Fact]
    public void Evaluate_SaldoAFavorNegativeSigned_AmbiguousZone_ReturnsInsufficientData()
    {
        var rule = GetRule();
        //
        // Design: choose values where NEITHER normalised NOR raw gives a clean Δ=0,
        // but the RAW interpretation (subtracting a negative = adding) happens to land
        // within tolerance while normalised does not — classic ambiguous zone.
        //
        // pagos = 10,000.00
        // col[1..5] sum = 9,800.00
        // col[6] raw = -150  (negative print)
        //
        // Normalised:  components = 9,800 - 150 = 9,650  → Δ = |10,000 - 9,650| = 350  > 0.50 (fails normalised)
        // Raw (pre-fix): components = 9,800 - (-150) = 9,950  → Δ = |10,000 - 9,950| = 50   > 0.50 (also fails raw)
        //
        // Actually we want a case where raw PASSES (≤ tol) but normalised FAILS to trigger abstain.
        // pagos = 10,000.00
        // col[1..5] sum = 9,750.30  (Regulares = 9,750.30, rest = 0)
        // col[6] raw = -249.70  (negative)
        //
        // Normalised:  components = 9,750.30 - 249.70 = 9,500.60  → Δ = |10,000 - 9,500.60| = 499.40 > 0.50  FAIL
        // Raw (pre-fix): components = 9,750.30 - (-249.70) = 10,000.00 → Δ = 0.00 ≤ 0.50  PASS
        //
        // Rule should detect the ambiguity and return InsufficientData.
        var cells = new List<TableCell>
        {
            AmountCell(-10_000.00m),
            AmountCell(9_750.30m),  // regulares (all other components are 0)
            AmountCell(0m),
            AmountCell(0m),
            AmountCell(0m),
            AmountCell(0m),
            AmountCell(-249.70m)   // saldo-a-favor printed negative
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Ambiguous sign zone: normalised interpretation fails but raw interpretation passes — " +
            "rule must abstain (InsufficientData) rather than emit a false-Fail.");
    }

    /// <summary>
    /// When col[6] saldo-a-favor is negative and BOTH the normalised and raw interpretations
    /// fail tolerance, the rule should return Fail — there is a genuine discrepancy regardless
    /// of sign convention.
    /// </summary>
    [Fact]
    public void Evaluate_SaldoAFavorNegativeSigned_BothInterpretationsFail_ReturnsFail()
    {
        var rule = GetRule();
        //
        // pagos = 67,796.35; components (normalised) = 61,033.35 - 500 = 60,533.35 → Δ = 7,263 >> 0.50
        // components (raw)  = 61,033.35 - (-500) = 61,533.35 → Δ = 6,263 >> 0.50
        // Both fail → genuine mismatch → Fail
        var cells = new List<TableCell>
        {
            AmountCell(-67_796.35m),
            AmountCell(61_033.35m),
            AmountCell(0m),
            AmountCell(0m),
            AmountCell(0m),
            AmountCell(0m),
            AmountCell(-500.00m)   // saldo-a-favor negative — both interpretations still fail
        };
        var table20 = MakeSection20Table(cells);
        var model = ModelWith([table20]);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "When both sign interpretations produce a discrepancy beyond tolerance, " +
            "the rule must return Fail — there is a genuine §20 violation.");
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }
}
