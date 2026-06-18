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
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Rules;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Tests;

/// <summary>
/// Unit tests for the LAW-TYPO-BOLD mandated-bold fields rule (Story 12.2).
/// All tests are pure in-memory — no PDF fixtures required.
/// Rules are discovered via the same Scrutor DI path used in production.
/// </summary>
public sealed class MandatedBoldFieldsRuleTests
{
    // -----------------------------------------------------------------------
    // DI factory — mirrors production wiring
    // -----------------------------------------------------------------------

    private static IVecValidationRule GetRule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        var rule = sp.GetServices<IVecValidationRule>()
            .Single(r => r.CheckId == "LAW-TYPO-BOLD");
        return rule;
    }

    // -----------------------------------------------------------------------
    // Fixture helpers
    // -----------------------------------------------------------------------

    private const string ProductId = "TC-BOLD-TEST";

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static VecReferenceBundle Bundle() =>
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
    /// Creates a <see cref="TextTypographySample"/> with specific bold/font attributes
    /// at a known location.
    /// </summary>
    private static TextTypographySample BoldSample(
        string text, string fontName, double bottom, int page = 1) =>
        new(
            Text: text,
            PointSize: 10.0,
            FontName: fontName,
            IsBold: fontName.Contains("bold", StringComparison.OrdinalIgnoreCase),
            PageNumber: page,
            Locator: new FieldLocator(page, 20.0, bottom, 60.0, 10.0));

    /// <summary>
    /// Creates a <see cref="FieldLocator"/> with a known Bottom coordinate on page 1.
    /// </summary>
    private static FieldLocator LocatorAt(double bottom, int page = 1) =>
        new(page, 10.0, bottom, 100.0, 10.0);

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> with the given samples and PeriodSummary.
    /// </summary>
    private static StatementModel ModelWithSamples(
        IReadOnlyList<TextTypographySample> samples,
        TypographyExtractionStatus status = TypographyExtractionStatus.Extracted,
        PeriodSummary? periodSummary = null) =>
        new(
            clientName: ExtractedField<ExtractedClientName>.Missing(P1()),
            address: ExtractedField<ExtractedAddress>.Missing(P1()),
            branchNumber: ExtractedField<string>.Missing(P1()),
            cardNumber: ExtractedField<string>.Missing(P1()),
            clabe: ExtractedField<string>.Missing(P1()),
            clientNumber: ExtractedField<string>.Missing(P1()),
            rfc: ExtractedField<string>.Missing(P1()))
        {
            TypographySamples = samples,
            TypographyExtractionStatus = status,
            PeriodSummary = periodSummary,
        };

    /// <summary>
    /// Builds a minimal <see cref="PeriodSummary"/> where every mandated field is Missing
    /// unless the caller overrides via named parameters.
    /// </summary>
    private static PeriodSummary MinimalPeriodSummary(
        ExtractedField<DateOnly>? paymentDueDate = null,
        ExtractedField<decimal>? pagoParaNoGenerarIntereses = null,
        ExtractedField<decimal>? pagoMinimoMasMeses = null,
        ExtractedField<decimal>? adeudoPeriodoAnterior = null,
        ExtractedField<decimal>? saldoDeudorTotal = null,
        ExtractedField<decimal>? totalCargos = null,
        ExtractedField<decimal>? totalAbonos = null,
        ExtractedField<decimal>? tasa = null,
        ExtractedField<decimal>? cat = null) =>
        new(
            product: ExtractedField<string>.Missing(P1()),
            periodStart: ExtractedField<DateOnly>.Missing(P1()),
            periodCutDate: ExtractedField<DateOnly>.Missing(P1()),
            paymentDueDate: paymentDueDate ?? ExtractedField<DateOnly>.Missing(P1()),
            dayCountPrinted: ExtractedField<int>.Missing(P1()),
            dayCount: new DayCountVerification(null, null, false),
            pagoParaNoGenerarIntereses: pagoParaNoGenerarIntereses ?? ExtractedField<decimal>.Missing(P1()),
            pagoMinimo: ExtractedField<decimal>.Missing(P1()),
            pagoMinimoMasMeses: pagoMinimoMasMeses ?? ExtractedField<decimal>.Missing(P1()),
            tasa: tasa ?? ExtractedField<decimal>.Missing(P1()),
            cat: cat ?? ExtractedField<decimal>.Missing(P1()),
            saldoDeudorTotal: saldoDeudorTotal ?? ExtractedField<decimal>.Missing(P1()),
            creditoDisponible: ExtractedField<decimal>.Missing(P1()),
            adeudoPeriodoAnterior: adeudoPeriodoAnterior ?? ExtractedField<decimal>.Missing(P1()),
            totalCargos: totalCargos ?? ExtractedField<decimal>.Missing(P1()),
            totalAbonos: totalAbonos ?? ExtractedField<decimal>.Missing(P1()));

    private static VerificationContext CtxWithModel(StatementModel? model) =>
        new(
            bundle: Bundle(),
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(Bundle()),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);

    // -----------------------------------------------------------------------
    // Test (a): InsufficientData paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var ctx = CtxWithModel(model: null);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.CheckId.ShouldBe("LAW-TYPO-BOLD");
    }

    [Fact]
    public void Evaluate_TypographyExtractionStatusNotFound_ReturnsInsufficientData()
    {
        var model = ModelWithSamples(
            [],
            TypographyExtractionStatus.NotFound,
            MinimalPeriodSummary());
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_EmptyTypographySamples_ReturnsInsufficientData()
    {
        var model = ModelWithSamples(
            [],
            TypographyExtractionStatus.Extracted,
            MinimalPeriodSummary());
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_NullPeriodSummary_ReturnsInsufficientData()
    {
        var samples = new List<TextTypographySample>
        {
            BoldSample("Saldo", "Aptos-Bold", 400.0),
        };
        var model = ModelWithSamples(samples, periodSummary: null);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // Test (b): All mandated fields bold (FontName "Aptos-Bold") → Pass
    // All 9 fields at the same bottom with one bold sample — quorum (≥3) is met.
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_AllMandatedFieldsBold_ReturnsPass()
    {
        // Place each mandated field at a distinct vertical position.
        // Each has a typography sample in the same vertical band with FontName "Aptos-Bold".
        const double bottom = 400.0;
        var fieldLocator = LocatorAt(bottom);

        var paymentDueDate = ExtractedField<DateOnly>.Found(
            new DateOnly(2025, 8, 25), fieldLocator);
        var pagoParaNoGenerarIntereses = ExtractedField<decimal>.Found(32446.69m, fieldLocator);
        var pagoMinimoMasMeses = ExtractedField<decimal>.Found(3145.39m, fieldLocator);
        var adeudoPeriodoAnterior = ExtractedField<decimal>.Found(29000m, fieldLocator);
        var saldoDeudorTotal = ExtractedField<decimal>.Found(32446.69m, fieldLocator);
        var totalCargos = ExtractedField<decimal>.Found(5000m, fieldLocator);
        var totalAbonos = ExtractedField<decimal>.Found(1000m, fieldLocator);
        var tasa = ExtractedField<decimal>.Found(0.1975m, fieldLocator);
        var cat = ExtractedField<decimal>.Found(0.26m, fieldLocator);

        var summary = MinimalPeriodSummary(
            paymentDueDate: paymentDueDate,
            pagoParaNoGenerarIntereses: pagoParaNoGenerarIntereses,
            pagoMinimoMasMeses: pagoMinimoMasMeses,
            adeudoPeriodoAnterior: adeudoPeriodoAnterior,
            saldoDeudorTotal: saldoDeudorTotal,
            totalCargos: totalCargos,
            totalAbonos: totalAbonos,
            tasa: tasa,
            cat: cat);

        // One bold sample on the same line covers all fields (same bottom, page 1).
        var samples = new List<TextTypographySample>
        {
            BoldSample("valor", "Aptos-Bold", bottom),
        };

        var model = ModelWithSamples(samples, periodSummary: summary);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.CheckId.ShouldBe("LAW-TYPO-BOLD");
        result.Value.Observed.ShouldNotBeNullOrEmpty();
    }

    // -----------------------------------------------------------------------
    // Test (c): One mandated field with explicit non-bold weight token → Fail
    // CHANGED: was "Aptos" (bare family → previously NotBold, now Indeterminate → not Fail).
    // Now uses "Aptos-Regular" which carries the explicit "-Regular" token → confident NotBold.
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_OneMandatedFieldWithExplicitRegularToken_ReturnsFail()
    {
        // "Aptos-Regular" carries the explicit "-Regular" weight token → ClassifyWeight → NotBold.
        const double bottom = 300.0;
        var fieldLocator = LocatorAt(bottom);

        var paymentDueDate = ExtractedField<DateOnly>.Found(
            new DateOnly(2025, 8, 25), fieldLocator);

        var summary = MinimalPeriodSummary(paymentDueDate: paymentDueDate);

        var samples = new List<TextTypographySample>
        {
            BoldSample("25-ago-2025", "Aptos-Regular", bottom), // explicit non-bold token → NotBold
        };

        var model = ModelWithSamples(samples, periodSummary: summary);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Expected.ShouldBe("negrillas (bold)");
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ShouldContain("fecha límite de pago");
        finding.Observed.ShouldContain("Aptos-Regular");
        finding.Locator.ShouldNotBeNull();
    }

    // -----------------------------------------------------------------------
    // Test (d): Mangled subset name "ABCDEE+QWERTY" → Indeterminate
    //           If it's the ONLY located field → InsufficientData (NOT Fail)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_MandatedFieldWithMangledSubsetName_ReturnsInsufficientData()
    {
        // "ABCDEE+QWERTY" — 6-letter prefix + garbage family, no weight token → Indeterminate.
        const double bottom = 350.0;
        var fieldLocator = LocatorAt(bottom);

        var paymentDueDate = ExtractedField<DateOnly>.Found(
            new DateOnly(2025, 8, 25), fieldLocator);

        var summary = MinimalPeriodSummary(paymentDueDate: paymentDueDate);

        var samples = new List<TextTypographySample>
        {
            BoldSample("25-ago-2025", "ABCDEE+QWERTY", bottom),
        };

        var model = ModelWithSamples(samples, periodSummary: summary);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        // Only one field was located and it was Indeterminate → no confident verdict → InsufficientData.
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        // Critically: must NOT be Fail.
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail);
    }

    // -----------------------------------------------------------------------
    // Test (e): NotExtracted field is skipped (does NOT cause Fail)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NotExtractedFieldSkipped_DoesNotFail()
    {
        // paymentDueDate is Missing (NotExtracted) — must be ignored.
        // No other mandated field is set up with a sample → InsufficientData (not Fail).
        const double bottom = 300.0;
        var samples = new List<TextTypographySample>
        {
            // Sample at a location for the Missing paymentDueDate locator (PageHint = no Bottom).
            // This sample won't be joined because the field locator has no Bottom.
            BoldSample("anything", "Aptos-Regular", bottom),
        };

        var summary = MinimalPeriodSummary(); // all fields Missing/NotExtracted

        var model = ModelWithSamples(samples, periodSummary: summary);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        // All mandated fields are NotExtracted → no confident verdict → InsufficientData.
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        // Critically: must NOT be Fail.
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail);
    }

    // -----------------------------------------------------------------------
    // Test (f): Mixed — most bold, one confidently NotBold → Fail naming that field
    // CHANGED: "Arial" (bare family) is now Indeterminate (not NotBold).
    // Now uses "Arial-Regular" (explicit token) for the confident NotBold field.
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_MostBoldOneNotBold_FailsNamingOffender()
    {
        // Three mandated fields:
        // (1) paymentDueDate at bottom=500 → "Aptos-Bold" → Bold
        // (2) pagoParaNoGenerarIntereses at bottom=450 → "Aptos-Bold" → Bold
        // (3) saldoDeudorTotal at bottom=400 → "Arial-Regular" → NotBold (explicit token)
        var boldLocator1 = LocatorAt(500.0);
        var boldLocator2 = LocatorAt(450.0);
        var notBoldLocator = LocatorAt(400.0);

        var paymentDueDate = ExtractedField<DateOnly>.Found(new DateOnly(2025, 8, 25), boldLocator1);
        var pagoParaNoGenerarIntereses = ExtractedField<decimal>.Found(32446.69m, boldLocator2);
        var saldoDeudorTotal = ExtractedField<decimal>.Found(32446.69m, notBoldLocator);

        var summary = MinimalPeriodSummary(
            paymentDueDate: paymentDueDate,
            pagoParaNoGenerarIntereses: pagoParaNoGenerarIntereses,
            saldoDeudorTotal: saldoDeudorTotal);

        var samples = new List<TextTypographySample>
        {
            BoldSample("25-ago-2025", "Aptos-Bold",     500.0),    // Bold — paymentDueDate line
            BoldSample("32446.69",    "Aptos-Bold",     450.0),    // Bold — pagoParaNoGenerarIntereses line
            BoldSample("32446.69",    "Arial-Regular",  400.0),    // NotBold — saldoDeudorTotal line
        };

        var model = ModelWithSamples(samples, periodSummary: summary);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ShouldContain("saldo deudor total");
        finding.Observed.ShouldContain("Arial-Regular");
    }

    // -----------------------------------------------------------------------
    // Additional: ClassifyWeight unit tests
    // CHANGED: bare known-family names ("Aptos", "Arial", "Helvetica", "Calibri",
    //   "TimesNewRoman") now → Indeterminate (not NotBold), because a bare family
    //   name carries no explicit non-bold weight token.
    //   Only an explicit token like "-Regular", "-Light" etc. → NotBold.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Aptos-Bold", WeightClass.Bold)]
    [InlineData("ABCDEF+Arial-Bold", WeightClass.Bold)]
    [InlineData("BlackItalic", WeightClass.Bold)]
    [InlineData("HeavyText", WeightClass.Bold)]
    [InlineData("SemiBoldVariant", WeightClass.Bold)]
    // Bare family names — no weight token → Indeterminate (CHANGED from NotBold).
    [InlineData("Aptos", WeightClass.Indeterminate)]
    [InlineData("Arial", WeightClass.Indeterminate)]
    [InlineData("Helvetica", WeightClass.Indeterminate)]
    [InlineData("Calibri", WeightClass.Indeterminate)]
    [InlineData("TimesNewRoman", WeightClass.Indeterminate)]
    // Explicit non-bold tokens → NotBold.
    [InlineData("ABCDEF+Aptos-Regular", WeightClass.NotBold)]
    [InlineData("Aptos-Light", WeightClass.NotBold)]
    [InlineData("Arial-Regular", WeightClass.NotBold)]
    [InlineData("ArialMT-Regular", WeightClass.NotBold)]
    // Mangled/unknown names → Indeterminate.
    [InlineData("ABCDEE+QWERTY", WeightClass.Indeterminate)]
    [InlineData("XF1234+ZXQ9", WeightClass.Indeterminate)]
    [InlineData("", WeightClass.Indeterminate)]
    [InlineData("   ", WeightClass.Indeterminate)]
    internal void ClassifyWeight_GivenFontName_ReturnsExpectedClass(
        string fontName, WeightClass expected)
    {
        var result = MandatedBoldFieldsRule.ClassifyWeight(fontName);
        result.ShouldBe(expected);
    }

    // -----------------------------------------------------------------------
    // Additional: Cancellation → Cancelled result
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Cancelled_ReturnsCancelledResult()
    {
        var rule = GetRule();
        var ctx = CtxWithModel(model: null);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Additional: DI registration — rule discovered by Scrutor
    // -----------------------------------------------------------------------

    [Fact]
    public void AddVeriqanVisual_PlusAddVeriqanValidation_ContainsLawTypoBoldRule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        var rules = sp.GetServices<IVecValidationRule>().ToList();

        rules.ShouldContain(
            r => r.CheckId == "LAW-TYPO-BOLD",
            "Expected LAW-TYPO-BOLD to be discovered by Scrutor.");
    }

    // -----------------------------------------------------------------------
    // Additional: DI uniqueness — no two rules share the same CheckId
    // -----------------------------------------------------------------------

    [Fact]
    public void AddVeriqanVisual_AllRules_HaveUniqueCheckIds()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        var rules = sp.GetServices<IVecValidationRule>().ToList();

        var duplicates = rules
            .GroupBy(r => r.CheckId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        duplicates.ShouldBeEmpty($"Duplicate CheckId(s): {string.Join(", ", duplicates)}");
    }

    // -----------------------------------------------------------------------
    // Additional: Band-join — sample outside tolerance is not joined
    // Tolerance is now 5 pt; the sample is 10 pt away → still not joined.
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_SampleOutsideTolerance_IsNotJoined_ReturnsInsufficientData()
    {
        // Field at bottom=400, sample at bottom=410 (distance=10 > SameLineTolerance=5).
        const double fieldBottom = 400.0;
        const double sampleBottom = 410.0;

        var fieldLocator = LocatorAt(fieldBottom);
        var paymentDueDate = ExtractedField<DateOnly>.Found(new DateOnly(2025, 8, 25), fieldLocator);

        var summary = MinimalPeriodSummary(paymentDueDate: paymentDueDate);

        var samples = new List<TextTypographySample>
        {
            // This sample is too far from the field locator (10 pt > 5 pt tolerance) — must not be joined.
            BoldSample("Aptos", "Aptos-Regular", sampleBottom),
        };

        var model = ModelWithSamples(samples, periodSummary: summary);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        // No samples were joined → field is NotLocated → InsufficientData.
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail);
    }

    // -----------------------------------------------------------------------
    // Additional: Mixed bold + indeterminate on same band → field is Bold (ANY bold wins)
    // CHANGED: with quorum=3 and only 1 extracted field (paymentDueDate), boldCount=1 < 3.
    // Now uses 3 extracted fields all bold so quorum is met → Pass.
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_BoldAndIndeterminateSamplesOnBand_QuorumMet_FieldClassifiedAsBold()
    {
        const double bottom1 = 500.0;
        const double bottom2 = 450.0;
        const double bottom3 = 400.0;

        var locator1 = LocatorAt(bottom1);
        var locator2 = LocatorAt(bottom2);
        var locator3 = LocatorAt(bottom3);

        var paymentDueDate = ExtractedField<DateOnly>.Found(new DateOnly(2025, 8, 25), locator1);
        var pagoParaNoGenerarIntereses = ExtractedField<decimal>.Found(32446.69m, locator2);
        var pagoMinimoMasMeses = ExtractedField<decimal>.Found(3145.39m, locator3);

        var summary = MinimalPeriodSummary(
            paymentDueDate: paymentDueDate,
            pagoParaNoGenerarIntereses: pagoParaNoGenerarIntereses,
            pagoMinimoMasMeses: pagoMinimoMasMeses);

        var samples = new List<TextTypographySample>
        {
            // Field 1: one bold sample + one indeterminate in band → bold wins (ANY bold rule).
            BoldSample("25-ago-2025", "ABCDEF+Aptos-Bold", bottom1),     // Bold
            BoldSample("etiqueta",    "ABCDEE+QWERTY",     bottom1 + 1), // Indeterminate (within 5 pt tolerance)
            // Field 2: bold sample.
            BoldSample("32446.69", "Aptos-Bold", bottom2),
            // Field 3: bold sample.
            BoldSample("3145.39", "Aptos-Bold", bottom3),
        };

        var model = ModelWithSamples(samples, periodSummary: summary);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        // All 3 fields are Bold, quorum (≥3) is met → Pass.
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    // -----------------------------------------------------------------------
    // NEW Test (quorum): Only 1 bold field located, rest NotExtracted → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_OnlyOneBoldFieldLocated_RestNotExtracted_ReturnsInsufficientData()
    {
        // Only paymentDueDate is extracted (bold). The other 8 mandated fields are Missing.
        // boldCount = 1 < QuorumThreshold (3) → InsufficientData.
        const double bottom = 400.0;
        var fieldLocator = LocatorAt(bottom);
        var paymentDueDate = ExtractedField<DateOnly>.Found(new DateOnly(2025, 8, 25), fieldLocator);

        var summary = MinimalPeriodSummary(paymentDueDate: paymentDueDate);

        var samples = new List<TextTypographySample>
        {
            BoldSample("25-ago-2025", "Aptos-Bold", bottom),
        };

        var model = ModelWithSamples(samples, periodSummary: summary);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        // Only 1 bold field found — quorum not met.
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Pass);
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail);
    }

    // -----------------------------------------------------------------------
    // NEW Test (bare-family): Bare family name → Indeterminate → InsufficientData (not Fail)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_BareFamilyNameMandatedField_ReturnsInsufficientData_NotFail()
    {
        // "Aptos" bare — no weight token — ClassifyWeight → Indeterminate.
        // With only 1 indeterminate field → InsufficientData, NOT Fail.
        const double bottom = 300.0;
        var fieldLocator = LocatorAt(bottom);
        var paymentDueDate = ExtractedField<DateOnly>.Found(new DateOnly(2025, 8, 25), fieldLocator);

        var summary = MinimalPeriodSummary(paymentDueDate: paymentDueDate);

        var samples = new List<TextTypographySample>
        {
            BoldSample("25-ago-2025", "Aptos", bottom), // bare family, no token → Indeterminate
        };

        var model = ModelWithSamples(samples, periodSummary: summary);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        // A bare family name must NOT produce Fail — it's Indeterminate → InsufficientData.
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail);
    }

    // -----------------------------------------------------------------------
    // NEW Test (explicit -Regular token): Mandated field with "ArialMT-Regular" → Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_ExplicitRegularTokenMandatedField_ReturnsFail()
    {
        // "ArialMT-Regular" has the explicit "-Regular" non-bold token → ClassifyWeight → NotBold.
        // A mandated bold field that is confidently NotBold → Fail.
        const double bottom = 350.0;
        var fieldLocator = LocatorAt(bottom);
        var paymentDueDate = ExtractedField<DateOnly>.Found(new DateOnly(2025, 8, 25), fieldLocator);

        var summary = MinimalPeriodSummary(paymentDueDate: paymentDueDate);

        var samples = new List<TextTypographySample>
        {
            BoldSample("25-ago-2025", "ArialMT-Regular", bottom),
        };

        var model = ModelWithSamples(samples, periodSummary: summary);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        // Explicit non-bold token → NotBold → Fail regardless of quorum.
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ShouldContain("ArialMT-Regular");
    }

    // -----------------------------------------------------------------------
    // NEW Test (3 bold fields meet quorum): Exactly QuorumThreshold bold → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_ExactlyThreeBoldFieldsLocated_ReturnsPass()
    {
        // Exactly 3 extracted bold fields → quorum met → Pass.
        var loc1 = LocatorAt(500.0);
        var loc2 = LocatorAt(450.0);
        var loc3 = LocatorAt(400.0);

        var paymentDueDate = ExtractedField<DateOnly>.Found(new DateOnly(2025, 8, 25), loc1);
        var pagoParaNoGenerarIntereses = ExtractedField<decimal>.Found(32446.69m, loc2);
        var pagoMinimoMasMeses = ExtractedField<decimal>.Found(3145.39m, loc3);

        var summary = MinimalPeriodSummary(
            paymentDueDate: paymentDueDate,
            pagoParaNoGenerarIntereses: pagoParaNoGenerarIntereses,
            pagoMinimoMasMeses: pagoMinimoMasMeses);

        var samples = new List<TextTypographySample>
        {
            BoldSample("valor1", "Aptos-Bold", 500.0),
            BoldSample("valor2", "Aptos-Bold", 450.0),
            BoldSample("valor3", "Aptos-Bold", 400.0),
        };

        var model = ModelWithSamples(samples, periodSummary: summary);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.Verdict.ShouldNotBe(FindingVerdict.InsufficientData);
    }
}
