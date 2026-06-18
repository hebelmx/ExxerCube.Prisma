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
/// Unit and integration tests for the Story 11.5 §8 "Indicadores del costo anual de la tarjeta"
/// rule (<c>LAW-§8-INDICADORES</c>).
/// </summary>
/// <remarks>
/// <para>
/// §8 mandates three 12-month cost indicators in every VEC credit-card statement:
/// <list type="bullet">
///   <item>[0] Monto de intereses pagados en los últimos 12 meses.</item>
///   <item>[1] Monto de comisiones totales pagadas en los últimos 12 meses.</item>
///   <item>[2] Monto de anualidad o comisiones por administración pagadas en los últimos 12 meses.</item>
/// </list>
/// </para>
/// <para>
/// <b>Fail-vs-InsufficientData decision logic:</b>
/// <list type="bullet">
///   <item>
///     <c>SectionNotFound</c> → InsufficientData (heading detection is heuristic; cannot
///     distinguish absent from undetected — abstain to prevent a false Fail).
///   </item>
///   <item>
///     <c>Indeterminate</c> / <c>NoRowsParsed</c> → InsufficientData (heading found but rows not reliably
///     reconstructed; abstain to prevent a false Fail).
///   </item>
///   <item>
///     <c>Extracted</c>, indicator value cell is <see cref="CellKind.Empty"/> → Fail (bank published §8
///     but omitted a legally-mandated value).
///   </item>
///   <item>
///     <c>Extracted</c>, value cell has no <c>ParsedValue</c> or low confidence → InsufficientData
///     (OCR could not read the number; abstain rather than risk a false Fail).
///   </item>
///   <item>
///     All three indicators present, non-negative → Pass.
///   </item>
///   <item>
///     Any indicator negative → Fail (12-month cumulative costs must be ≥ 0).
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Cardinal rule:</b> when in doubt between Fail and InsufficientData, choose InsufficientData.
/// A false Fail halts a bank's billing run.
/// </para>
/// </remarks>
public sealed class Section8IndicatorsRuleTests
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    private const decimal Tol = 0.50m;   // CurrencyToleranceMxn legal default
    private const string ProductId = "TC-S8-TEST";
    private const string CheckId = "LAW-§8-INDICADORES";
    private const string SectionName = "Indicadores del costo anual de la tarjeta";

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
    /// Creates a §8 <see cref="FinancialTable"/> (Extracted) from the 3 provided value cells
    /// (one per row, each with a descriptive row label).
    /// </summary>
    private static FinancialTable MakeSection8Table(
        TableCell indicator0,
        TableCell indicator1,
        TableCell indicator2)
    {
        var labels = new[]
        {
            "Intereses pagados en los últimos 12 meses",
            "Comisiones totales pagadas en los últimos 12 meses",
            "Anualidad o comisiones por administración pagadas en los últimos 12 meses",
        };
        var rows = new List<TableRow>
        {
            new(TableCell.LabelCell(labels[0], P1()), new[] { indicator0 }),
            new(TableCell.LabelCell(labels[1], P1()), new[] { indicator1 }),
            new(TableCell.LabelCell(labels[2], P1()), new[] { indicator2 }),
        };
        return new FinancialTable(
            sectionNumber: 8,
            sectionName: SectionName,
            status: TableExtractionStatus.Extracted,
            rows: rows,
            locator: P1());
    }

    /// <summary>
    /// Builds a <see cref="StatementModel"/> (all header fields Missing) with the supplied table
    /// injected into <c>FinancialTables</c>.
    /// </summary>
    private static StatementModel ModelWith(FinancialTable table8)
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
            FinancialTables = [table8],
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
    // Rule metadata contract
    // -----------------------------------------------------------------------

    [Fact]
    public void Rule_Metadata_HasExpectedCheckIdAndClassification()
    {
        var rule = GetRule();

        rule.CheckId.ShouldBe(CheckId);
        rule.DofNumeral.ShouldBe("Acuerdo §8");
        rule.Classification.ShouldBe(RuleClassification.TenantTightenableOnly);
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }

    // -----------------------------------------------------------------------
    // (a) All 3 indicators present and non-negative → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_ThreeNonNegativeIndicators_ReturnsPass()
    {
        var table = MakeSection8Table(
            TableCell.Amount(1_200.00m, "1200.00", P1()),
            TableCell.Amount(450.00m, "450.00", P1()),
            TableCell.Amount(0.00m, "0.00", P1()));   // zero is valid (non-negative)
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(CheckId);
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(Tol);
        result.Value.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass);
    }

    /// <summary>
    /// All three indicators present; second indicator is exactly zero (still valid).
    /// </summary>
    [Fact]
    public void Evaluate_IndicatorIsZero_IsNonNegative_ReturnsPass()
    {
        var table = MakeSection8Table(
            TableCell.Amount(800.00m, "800.00", P1()),
            TableCell.Amount(0.00m, "0.00", P1()),
            TableCell.Amount(300.00m, "300.00", P1()));
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Zero is a valid (non-negative) indicator value");
    }

    // -----------------------------------------------------------------------
    // (b) One indicator value cell Empty (present-section, absent-value) → Fail citing §8
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_FirstIndicatorEmpty_ReturnsFail_CitingSection8()
    {
        var table = MakeSection8Table(
            TableCell.EmptyCell(P1()),                      // indicator [0] absent
            TableCell.Amount(450.00m, "450.00", P1()),
            TableCell.Amount(300.00m, "300.00", P1()));
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed.ShouldNotBeNullOrEmpty("Fail must carry a reason in Observed");
        result.Value.Observed.ShouldContain("§8", Case.Sensitive);
        result.Value.LegalBaselineVerdict.ShouldBe(FindingVerdict.Fail);
    }

    /// <summary>
    /// TableCell.Missing() returns Kind=Empty, Confidence=0.0.
    /// Since 0.0 &lt; 0.8 (legal confidence threshold), a Missing cell is treated as
    /// InsufficientData — we cannot distinguish a genuinely absent indicator from an
    /// OCR failure that produced a zero-confidence read. Abstain rather than false-Fail.
    /// </summary>
    [Fact]
    public void Evaluate_SecondIndicatorMissing_ReturnsInsufficientData()
    {
        var table = MakeSection8Table(
            TableCell.Amount(1_200.00m, "1200.00", P1()),
            TableCell.Missing(P1()),                        // indicator [1]: confidence=0.0 → InsufficientData
            TableCell.Amount(300.00m, "300.00", P1()));
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Missing cell has confidence 0.0 < 0.8 threshold — cannot distinguish absent from OCR failure; must abstain");
        result.Value.Observed.ShouldNotBeNullOrEmpty("InsufficientData must carry a reason");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Cardinal rule: never false-Fail on zero-confidence extraction");
    }

    [Fact]
    public void Evaluate_ThirdIndicatorEmpty_ReturnsFail_CitingSection8()
    {
        var table = MakeSection8Table(
            TableCell.Amount(1_200.00m, "1200.00", P1()),
            TableCell.Amount(450.00m, "450.00", P1()),
            TableCell.EmptyCell(P1()));                     // indicator [2] absent
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Observed.ShouldNotBeNullOrEmpty("Fail must carry a reason in Observed");
        result.Value.Observed!.ShouldContain("§8", Case.Sensitive);
    }

    // -----------------------------------------------------------------------
    // (c) Negative indicator value → Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NegativeFirstIndicator_ReturnsFail()
    {
        var table = MakeSection8Table(
            TableCell.Amount(-100.00m, "-100.00", P1()),    // negative — invalid
            TableCell.Amount(450.00m, "450.00", P1()),
            TableCell.Amount(300.00m, "300.00", P1()));
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "Negative indicator values are legally invalid (12-month costs cannot be negative)");
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Evaluate_NegativeSecondIndicator_ReturnsFail()
    {
        var table = MakeSection8Table(
            TableCell.Amount(1_200.00m, "1200.00", P1()),
            TableCell.Amount(-0.01m, "-0.01", P1()),        // just barely negative
            TableCell.Amount(300.00m, "300.00", P1()));
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    [Fact]
    public void Evaluate_NegativeThirdIndicator_ReturnsFail()
    {
        var table = MakeSection8Table(
            TableCell.Amount(1_200.00m, "1200.00", P1()),
            TableCell.Amount(450.00m, "450.00", P1()),
            TableCell.Amount(-500.00m, "-500.00", P1()));   // negative
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
    }

    // -----------------------------------------------------------------------
    // (d) Low-confidence indicator → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_LowConfidenceFirstIndicator_ReturnsInsufficientData()
    {
        // Confidence 0.5 — below the 0.8 legal default threshold.
        var lowConf = new TableCell("1200.00", 1_200.00m, CellKind.Amount, 0.5, P1());
        var table = MakeSection8Table(
            lowConf,
            TableCell.Amount(450.00m, "450.00", P1()),
            TableCell.Amount(300.00m, "300.00", P1()));
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "A low-confidence cell must cause abstain, never a Fail or Pass verdict");
    }

    [Fact]
    public void Evaluate_ParseFailureCell_ReturnsInsufficientData()
    {
        // ParseFailure: text present but numeric parse failed → ParsedValue is null, confidence 0.7.
        var table = MakeSection8Table(
            TableCell.Amount(1_200.00m, "1200.00", P1()),
            TableCell.ParseFailure("???", P1()),            // indicator [1] unreadable
            TableCell.Amount(300.00m, "300.00", P1()));
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "A parse-failed cell (no parsed value) must cause abstain");
    }

    // -----------------------------------------------------------------------
    // (e) §8 Status == Indeterminate / NoRowsParsed → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section8StatusIndeterminate_ReturnsInsufficientData()
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
            FinancialTables = [FinancialTable.Indeterminate(8, SectionName, P1())]
        };
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Indeterminate status means we cannot tell if indicators are absent or unreadable — must abstain");
        result.Value.Observed.ShouldNotBeNullOrEmpty("InsufficientData must carry a reason");
    }

    [Fact]
    public void Evaluate_Section8StatusNoRowsParsed_ReturnsInsufficientData()
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
            FinancialTables = [FinancialTable.NoRows(8, SectionName, P1())]
        };
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "NoRowsParsed means the heading was found but rows are missing — abstain");
    }

    // -----------------------------------------------------------------------
    // (f) §8 SectionNotFound → InsufficientData
    //     (heading detection is heuristic; cannot distinguish absent from undetected)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section8SectionNotFound_ReturnsInsufficientData()
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
            FinancialTables = [FinancialTable.NotFound(8, SectionName)]
        };
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "SectionNotFound uses heuristic heading detection — cannot distinguish absent from undetected; must abstain");
        result.Value.Observed.ShouldNotBeNullOrEmpty("InsufficientData must carry a reason");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Cardinal rule: never false-Fail on a billing-run gate when detection is heuristic");
    }

    // -----------------------------------------------------------------------
    // (f2) Low-confidence Empty cell → InsufficientData (not Fail)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_LowConfidenceEmptyCell_ReturnsInsufficientData_NotFail()
    {
        // An Empty cell with confidence below threshold — OCR may have simply missed
        // the value. We must not Fail on an uncertain read.
        var lowConfEmpty = new TableCell(string.Empty, null, CellKind.Empty, 0.3, P1());
        var table = MakeSection8Table(
            lowConfEmpty,                                    // indicator [0]: low-conf empty
            TableCell.Amount(450.00m, "450.00", P1()),
            TableCell.Amount(300.00m, "300.00", P1()));
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Empty cell with confidence 0.3 < 0.8 threshold must abstain, not Fail");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Cardinal rule: never false-Fail on uncertain extraction");
    }

    // -----------------------------------------------------------------------
    // (f3) Unregistered tolerance → InsufficientData (not exception)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_UnregisteredTolerance_ReturnsInsufficientData_NotException()
    {
        // Arrange: build a rule with a tolerance provider that has no entry for CheckId.
        var rule = new Section8AnnualCostIndicatorsRule(new EmptyToleranceProvider());

        var table = MakeSection8Table(
            TableCell.Amount(500.00m, "500.00", P1()),
            TableCell.Amount(200.00m, "200.00", P1()),
            TableCell.Amount(100.00m, "100.00", P1()));
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("result must be a success-wrapped InsufficientData, not an exception");
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Unregistered tolerance must return InsufficientData, never throw");
    }

    // -----------------------------------------------------------------------
    // Test stub: tolerance provider with no registered tolerances
    // -----------------------------------------------------------------------

    /// <summary>
    /// Stub <see cref="ILegalToleranceProvider"/> that reports no tolerances registered.
    /// Used to test the Has-guard in each rule (FIX 3).
    /// </summary>
    private sealed class EmptyToleranceProvider : ILegalToleranceProvider
    {
        public bool Has(string checkId) => false;

        public Tolerance For(string checkId) =>
            throw new InvalidOperationException($"No tolerance registered for '{checkId}'.");
    }

    // -----------------------------------------------------------------------
    // Null / absent StatementModel → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var ctx = Ctx(model: null);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Observed.ShouldNotBeNullOrEmpty("InsufficientData must carry a reason");
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
    // ToleranceApplied contract (ADR-V3)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Pass_RecordsToleranceApplied()
    {
        var table = MakeSection8Table(
            TableCell.Amount(500.00m, "500.00", P1()),
            TableCell.Amount(200.00m, "200.00", P1()),
            TableCell.Amount(100.00m, "100.00", P1()));
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.ToleranceApplied.ShouldBe(Tol,
            "ADR-V3: the applied tolerance must be recorded even on Pass findings");
    }

    // -----------------------------------------------------------------------
    // (g) Real-fixture integration test: jul_ago §8 was extracted with 3 indicators
    //     (confirmed by Story 11.1 spike) — must NEVER be a false Fail.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Constructs a synthetic §8 model that mirrors the jul_ago fixture's known state:
    /// §8 Status == Extracted, 3 rows, each with 1 value cell at confidence 1.0.
    /// The amounts are non-negative (12-month cost indicators from the dummy generator).
    /// The rule MUST produce Pass or InsufficientData — NEVER a false Fail.
    /// </summary>
    [Fact]
    public void Evaluate_JulAgoFixtureShape_ThreeExtractedIndicators_NeverFail()
    {
        // Story 11.1 spike confirmed: §8 in jul_ago has 3 rows, each 1 value cell,
        // Status == Extracted. The amounts are dummy-generated non-negative values.
        // We reproduce the shape synthetically so the test is deterministic and
        // does not depend on a real PDF in the test host.
        var table = MakeSection8Table(
            TableCell.Amount(2_400.00m, "2400.00", P1()),   // indicador [0] — intereses
            TableCell.Amount(600.00m, "600.00", P1()),      // indicador [1] — comisiones
            TableCell.Amount(900.00m, "900.00", P1()));     // indicador [2] — anualidad
        var model = ModelWith(table);
        var ctx = Ctx(model);

        var result = GetRule().Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe(CheckId);

        // Cardinal rule: NEVER a false Fail on a well-extracted §8 with non-negative values.
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "A billing-run gate must NEVER false-Fail when §8 has 3 extracted non-negative indicators");

        // The correct verdict for this shape is Pass (all indicators present, non-negative).
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass,
            "3 non-negative extracted indicators → Pass is the only correct verdict");
    }
}
