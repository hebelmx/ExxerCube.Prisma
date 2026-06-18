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
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Tests;

/// <summary>
/// Unit tests for the LAW-TYPO-MINSIZE typography point-size floor rule (Story 12.1).
/// All tests are pure in-memory — no PDF fixtures required.
/// Rules are discovered via the same Scrutor DI path used in production.
/// </summary>
public sealed class TypographyPointSizeFloorRuleTests
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
            .Single(r => r.CheckId == "LAW-TYPO-MINSIZE");
        return rule;
    }

    // -----------------------------------------------------------------------
    // Fixture helpers
    // -----------------------------------------------------------------------

    private const string ProductId = "TC-TYPO-TEST";

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
    /// Builds a minimal <see cref="StatementModel"/> with the given typography samples
    /// and extraction status.
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
    /// Creates a <see cref="TextTypographySample"/> at a given point size with a
    /// bounding box locator on page 1.
    /// </summary>
    private static TextTypographySample Sample(string text, double pointSize, int page = 1) =>
        new(
            Text: text,
            PointSize: pointSize,
            FontName: "Arial",
            IsBold: false,
            PageNumber: page,
            Locator: new FieldLocator(page, 20.0, 700.0, 50.0, pointSize));

    /// <summary>
    /// Creates a <see cref="TextTypographySample"/> with an explicit bottom (Y) coordinate,
    /// used for horizontal-line grouping tests.
    /// </summary>
    private static TextTypographySample SampleAt(
        string text, double pointSize, double left, double bottom, int page = 1) =>
        new(
            Text: text,
            PointSize: pointSize,
            FontName: "Arial",
            IsBold: false,
            PageNumber: page,
            Locator: new FieldLocator(page, left, bottom, 30.0, pointSize));

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
        result.Value.CheckId.ShouldBe("LAW-TYPO-MINSIZE");
    }

    [Fact]
    public void Evaluate_TypographyExtractionStatusNotFound_ReturnsInsufficientData()
    {
        var model = ModelWithSamples([], TypographyExtractionStatus.NotFound);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_EmptyTypographySamples_ReturnsInsufficientData()
    {
        // Even when status is Extracted, an empty list means no data.
        var model = ModelWithSamples([], TypographyExtractionStatus.Extracted);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // Test (b): Body ≥ 8 pt + fecha-límite ≥ 10 pt → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_AllBodyAboveFloor_FechaLimiteAboveFloor_ReturnsPass()
    {
        // Body text at 9 pt (above 8 pt floor).
        // Fecha-límite label on a line at 11 pt (above 10 pt floor).
        var fechaBottom = 500.0;
        var samples = new List<TextTypographySample>
        {
            Sample("Cliente", 9.0),
            Sample("Saldo", 10.0),
            // fecha-límite label words on the same horizontal line
            SampleAt("Fecha", 11.0, 10.0, fechaBottom),
            SampleAt("límite", 11.0, 50.0, fechaBottom),
            SampleAt("de", 11.0, 90.0, fechaBottom),
            SampleAt("pago", 11.0, 130.0, fechaBottom),
        };
        var model = ModelWithSamples(samples);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.CheckId.ShouldBe("LAW-TYPO-MINSIZE");
        result.Value.Observed.ShouldNotBeNullOrEmpty();
    }

    // -----------------------------------------------------------------------
    // Test (c): Real-word sample at 6 pt → Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_BodySampleAt6pt_ReturnsFail()
    {
        // "Beneficios" is a real word (trimmed length >= 2); 6 pt is below 8 pt - 0.25 floor.
        var samples = new List<TextTypographySample>
        {
            Sample("Normal", 9.0),
            Sample("Beneficios", 6.0), // violation
        };
        var model = ModelWithSamples(samples);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ShouldContain("6.00");
        finding.Locator.ShouldNotBeNull();
        finding.Expected.ShouldNotBeNullOrEmpty();
        finding.Expected!.ShouldContain("8");
    }

    // -----------------------------------------------------------------------
    // Test (d): Fecha-límite label line at 9 pt → Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_FechaLimiteLabelAt9pt_ReturnsFail()
    {
        // Body text is fine (10 pt), but the fecha-límite label is at 9 pt (< 10 - 0.25 floor).
        var fechaBottom = 500.0;
        var samples = new List<TextTypographySample>
        {
            Sample("Saldo", 10.0),
            // fecha-límite label at 9 pt — below the 10 pt floor
            SampleAt("Fecha", 9.0, 10.0, fechaBottom),
            SampleAt("límite", 9.0, 50.0, fechaBottom),
            SampleAt("de", 9.0, 90.0, fechaBottom),
            SampleAt("pago", 9.0, 130.0, fechaBottom),
        };
        var model = ModelWithSamples(samples);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ShouldContain("9.00");
        finding.Expected.ShouldNotBeNullOrEmpty();
        finding.Expected!.ShouldContain("10");
    }

    // -----------------------------------------------------------------------
    // Test (e): Single-char glyph (Text length < 2) does NOT cause Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_SingleCharGlyphBelowFloor_DoesNotFail()
    {
        // A single-character sample at 4 pt must be ignored (superscript, ®, etc.).
        // All real-word samples are above the floor.
        var samples = new List<TextTypographySample>
        {
            new("®", 4.0, "Arial", false, 1,
                new FieldLocator(1, 100.0, 750.0, 5.0, 4.0)),   // single char — must be ignored
            new("*", 3.0, "Arial", false, 1,
                new FieldLocator(1, 200.0, 750.0, 5.0, 3.0)),   // single char — must be ignored
            Sample("Texto", 9.0),  // real word — above floor
        };
        var model = ModelWithSamples(samples);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    // -----------------------------------------------------------------------
    // Test (f): Fecha-límite label absent but body OK → Pass (sub-check skipped)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_FechaLimiteLabelAbsent_BodyOk_ReturnsPass()
    {
        // No fecha-límite label tokens in the samples, body is fine.
        var samples = new List<TextTypographySample>
        {
            Sample("Saldo", 10.0),
            Sample("Anterior", 9.0),
            Sample("Crédito", 12.0),
        };
        var model = ModelWithSamples(samples);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        // The observed text should mention that the sub-check was skipped.
        result.Value.Observed.ShouldNotBeNullOrEmpty();
        result.Value.Observed!.ShouldContain("skipped");
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
    public void AddVeriqanVisual_PlusAddVeriqanValidation_ContainsLawTypoMinSizeRule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        var rules = sp.GetServices<IVecValidationRule>().ToList();

        rules.ShouldContain(
            r => r.CheckId == "LAW-TYPO-MINSIZE",
            "Expected LAW-TYPO-MINSIZE to be discovered by Scrutor.");
    }

    // -----------------------------------------------------------------------
    // Additional: Body exactly at floor (8.0 pt) → Pass (not a confident breach)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_BodyExactlyAtFloor_ReturnsPass()
    {
        // 8.0 pt is exactly the floor — Epsilon guards against this: 8.0 < 8.0 - 0.25 is false.
        var samples = new List<TextTypographySample>
        {
            Sample("Texto", 8.0),
        };
        var model = ModelWithSamples(samples);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    // -----------------------------------------------------------------------
    // Additional: Body just above Epsilon threshold (7.76 pt) → Fail
    //             Body in the Epsilon tolerance band (7.80 pt) → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_BodyJustBelowEpsilonThreshold_ReturnsFail()
    {
        // 7.74 pt < 8.0 - 0.25 = 7.75 → confident breach → Fail.
        var samples = new List<TextTypographySample>
        {
            Sample("Adeudo", 7.74),
        };
        var model = ModelWithSamples(samples);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
    }

    [Fact]
    public void Evaluate_BodyWithinEpsilonTolerance_ReturnsPass()
    {
        // 7.80 pt is NOT < 7.75 (floor - epsilon) → not a confident breach → Pass.
        var samples = new List<TextTypographySample>
        {
            Sample("Adeudo", 7.80),
        };
        var model = ModelWithSamples(samples);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    // -----------------------------------------------------------------------
    // Additional: Fecha-límite located via PeriodSummary.PaymentDueDate field
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_FechaLimiteLocatedViaPeriodSummary_BelowFloor_ReturnsFail()
    {
        // PaymentDueDate is extracted with a locator on page 1 at bottom=400.
        // A typography sample on page 1 near bottom=400 is at 8 pt (below 10 pt floor).
        var labelLocator = new FieldLocator(1, 10.0, 400.0, 120.0, 10.0);
        var paymentDueDate = ExtractedField<DateOnly>.Found(
            new DateOnly(2025, 8, 25), labelLocator);

        var periodSummary = new PeriodSummary(
            product: ExtractedField<string>.Missing(P1()),
            periodStart: ExtractedField<DateOnly>.Missing(P1()),
            periodCutDate: ExtractedField<DateOnly>.Missing(P1()),
            paymentDueDate: paymentDueDate,
            dayCountPrinted: ExtractedField<int>.Missing(P1()),
            dayCount: new DayCountVerification(null, null, false),
            pagoParaNoGenerarIntereses: ExtractedField<decimal>.Missing(P1()),
            pagoMinimo: ExtractedField<decimal>.Missing(P1()),
            pagoMinimoMasMeses: ExtractedField<decimal>.Missing(P1()),
            tasa: ExtractedField<decimal>.Missing(P1()),
            cat: ExtractedField<decimal>.Missing(P1()),
            saldoDeudorTotal: ExtractedField<decimal>.Missing(P1()),
            creditoDisponible: ExtractedField<decimal>.Missing(P1()));

        // Body text is fine at 10 pt; the fecha-límite region has a sample at 8 pt (breach).
        var samples = new List<TextTypographySample>
        {
            Sample("Saldo", 10.0),          // body — OK
            // Sample near the PaymentDueDate locator bottom (400 ± 3 pt tolerance).
            SampleAt("límite", 8.0, 30.0, 401.0),   // fecha-límite area — breach
        };

        var model = ModelWithSamples(samples, periodSummary: periodSummary);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Expected.ShouldNotBeNullOrEmpty();
        finding.Expected!.ShouldContain("10");
    }

    // -----------------------------------------------------------------------
    // Additional: Accent-stripped phrase matching — "Fecha límite de pago"
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_FechaLimiteWithAccents_IsLocatedCorrectly()
    {
        // "límite" has an accent; the phrase scanner must strip it to "limite" and match.
        var fechaBottom = 600.0;
        var samples = new List<TextTypographySample>
        {
            Sample("Saldo", 10.0),
            // Phrase with accented "límite" — must still be found.
            SampleAt("Fecha", 11.0, 10.0, fechaBottom),
            SampleAt("límite", 11.0, 55.0, fechaBottom),
            SampleAt("de", 11.0, 100.0, fechaBottom),
            SampleAt("pago", 11.0, 130.0, fechaBottom),
        };
        var model = ModelWithSamples(samples);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        // The phrase is at 11 pt (above 10 pt floor) → Pass, and sub-check is located.
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.Observed.ShouldNotBeNullOrEmpty();
        result.Value.Observed!.ShouldContain("located");
    }

    // -----------------------------------------------------------------------
    // Additional: Multiple body violations → count in Observed
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_MultipleBodyViolations_CountInObserved()
    {
        var samples = new List<TextTypographySample>
        {
            Sample("Normal", 9.0),
            Sample("Pequeño", 6.0),   // violation 1
            Sample("Micro", 5.0),     // violation 2
        };
        var model = ModelWithSamples(samples);
        var rule = GetRule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        // The observed message should mention the violation count (2).
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("2");
    }
}
