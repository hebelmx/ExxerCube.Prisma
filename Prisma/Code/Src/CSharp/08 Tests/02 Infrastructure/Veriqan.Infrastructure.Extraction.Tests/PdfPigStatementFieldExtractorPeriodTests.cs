using ExxerCube.Prisma.Veriqan.Application.Verification;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using IndQuestResults.Operations;
using Meziantou.Extensions.Logging.Xunit.v3;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story 3.2 integration tests for <see cref="PdfPigStatementFieldExtractor.ExtractFullAsync"/>:
/// period/summary field extraction, day-count verification, and TASA matching against
/// the reference bundle.
/// </summary>
/// <remarks>
/// Tests run against the real PDF text layer — no mocking.
/// Fixtures are the three Dummie VEC PDFs (jul-ago, ago-sep, sep-oct 2025).
/// </remarks>
public sealed class PdfPigStatementFieldExtractorPeriodTests
{
    // -----------------------------------------------------------------------
    // Fixture paths
    // -----------------------------------------------------------------------

    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FixturePath(string fileName) =>
        Path.Combine(FixturesDir, fileName);

    private static readonly string JulAgoFixture =
        FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");

    private static readonly string AgoSepFixture =
        FixturePath("02+Dummie+VEC+ago_sep+2025.pdf");

    private static readonly string SepOctFixture =
        FixturePath("03+Dummie+VEC+sep_oct+2025.pdf");

    // -----------------------------------------------------------------------
    // Factory helpers
    // -----------------------------------------------------------------------

    private static PdfPigStatementFieldExtractor CreateExtractor()
    {
        var logger = XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>();
        return new PdfPigStatementFieldExtractor(logger, Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()), new NullPasswordProvider());
    }

    private static byte[] ReadFixture(string path)
    {
        File.Exists(path).ShouldBeTrue($"Fixture not found at: {path}");
        return File.ReadAllBytes(path);
    }

    // -----------------------------------------------------------------------
    // Helper — extract PeriodSummary from fixture #1 (jul-ago)
    // -----------------------------------------------------------------------

    private static async Task<PeriodSummary> GetJulAgoPeriodSummaryAsync(CancellationToken ct)
    {
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);
        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync must succeed. Error: {result.Error}");

        var model = result.Value!;
        model.PeriodSummary.ShouldNotBeNull(
            "PeriodSummary must be populated by ExtractFullAsync");

        return model.PeriodSummary!;
    }

    // -----------------------------------------------------------------------
    // Test S3.2-1: Period start and cut dates parse correctly (fixture #1: jul-ago)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fixture #1 ("Periodo 5-jul-2025 al 04-ago-2025") must yield:
    ///   PeriodStart = 2025-07-05
    ///   PeriodCutDate = 2025-08-04  (from "Fecha de Corte 04 de ago 2025")
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_PeriodParsesIntoStartAndCutDates()
    {
        var ct = TestContext.Current.CancellationToken;
        var ps = await GetJulAgoPeriodSummaryAsync(ct);

        // Period start
        ps.PeriodStart.Status.ShouldBe(ExtractionStatus.Extracted, "PeriodStart must be extracted");
        ps.PeriodStart.Value.ShouldBe(
            new DateOnly(2025, 7, 5),
            "PeriodStart from '5-jul-2025' must be 2025-07-05");
        ps.PeriodStart.Locator.ShouldNotBeNull();
        ps.PeriodStart.Confidence.ShouldBe(1.0);

        // Cut date
        ps.PeriodCutDate.Status.ShouldBe(ExtractionStatus.Extracted, "PeriodCutDate must be extracted");
        ps.PeriodCutDate.Value.ShouldBe(
            new DateOnly(2025, 8, 4),
            "PeriodCutDate from '04 de ago 2025' must be 2025-08-04");
        ps.PeriodCutDate.Locator.ShouldNotBeNull();
        ps.PeriodCutDate.Confidence.ShouldBe(1.0);
    }

    // -----------------------------------------------------------------------
    // Test S3.2-2: Day-count verification
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fixture #1 prints "Número de días en el periodo: 31".
    /// Computed span (exclusive-end): 2025-08-04 − 2025-07-05 = 30 days.
    /// Inclusive-end reconciliation: 30 + 1 = 31 → IsConsistent must be true.
    /// Convention: DayCount.ComputedSpanDays is the exclusive-end span (30);
    ///             DayCount.PrintedDays is the inclusive-end value as printed (31);
    ///             IsConsistent = (PrintedDays == ComputedSpanDays + 1).
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_DayCountEqualsSpan()
    {
        var ct = TestContext.Current.CancellationToken;
        var ps = await GetJulAgoPeriodSummaryAsync(ct);

        // Printed value
        ps.DayCountPrinted.Status.ShouldBe(ExtractionStatus.Extracted,
            "DayCountPrinted must be extracted");
        ps.DayCountPrinted.Value.ShouldBe(31,
            "Fixture #1 prints 31 days");

        // Computed span (exclusive-end convention)
        ps.DayCount.ComputedSpanDays.ShouldNotBeNull("ComputedSpanDays must be computed from dates");
        ps.DayCount.ComputedSpanDays.ShouldBe(30,
            "Exclusive-end span: 2025-08-04 - 2025-07-05 = 30 calendar days");

        ps.DayCount.PrintedDays.ShouldBe(31, "PrintedDays must match the extracted printed value");

        // Inclusive-end reconciliation: 30 + 1 = 31 → consistent
        ps.DayCount.IsConsistent.ShouldBeTrue(
            "Printed 31 = span 30 + 1 (inclusive-end convention) → IsConsistent must be true");
    }

    // -----------------------------------------------------------------------
    // Test S3.2-3: Summary amounts
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fixture #1 summary amounts:
    ///   PagoParaNoGenerarIntereses = $32,446.69
    ///   PagoMinimoMasMeses         = $3,145.39
    ///   PagoMinimo                 = $2,160.00
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_SummaryAmountsExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var ps = await GetJulAgoPeriodSummaryAsync(ct);

        // Pago para no generar intereses
        ps.PagoParaNoGenerarIntereses.Status.ShouldBe(ExtractionStatus.Extracted,
            "PagoParaNoGenerarIntereses must be extracted");
        ps.PagoParaNoGenerarIntereses.Value.ShouldBe(32446.69m,
            "PagoParaNoGenerarIntereses must be $32,446.69");

        // Pago mínimo + compras y cargos diferidos a meses
        ps.PagoMinimoMasMeses.Status.ShouldBe(ExtractionStatus.Extracted,
            "PagoMinimoMasMeses must be extracted");
        ps.PagoMinimoMasMeses.Value.ShouldBe(3145.39m,
            "PagoMinimoMasMeses must be $3,145.39");

        // Pago mínimo (alone)
        ps.PagoMinimo.Status.ShouldBe(ExtractionStatus.Extracted,
            "PagoMinimo must be extracted");
        ps.PagoMinimo.Value.ShouldBe(2160.00m,
            "PagoMinimo must be $2,160.00");
    }

    // -----------------------------------------------------------------------
    // Test S3.2-4: Product name extracted
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fixture #1 product heading is "Tarjeta de Crédito BSSB".
    /// The extracted product field must contain "BSSB".
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_ProductExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var ps = await GetJulAgoPeriodSummaryAsync(ct);

        ps.Product.Status.ShouldBe(ExtractionStatus.Extracted,
            "Product must be extracted");
        ps.Product.Value.ShouldNotBeNullOrWhiteSpace("Product value must not be blank");
        ps.Product.Value!.ShouldContain("BSSB");
        ps.Product.Value.ShouldContain("Tarjeta");
    }

    // -----------------------------------------------------------------------
    // Test S3.2-5a: Rate match — extracted TASA matches bundle TASA
    // -----------------------------------------------------------------------

    /// <summary>
    /// Build a VecReferenceBundle whose TASA for product "TC-BSSB" / period "Jul Ago 2025"
    /// is set to the value printed in fixture #1 (extracted at runtime).
    /// TasaMatcher.Match must return Matched.
    /// Fixture #1 must yield an extracted TASA — if it does not, the test fails so
    /// we catch the regression immediately rather than silently deflecting.
    /// </summary>
    [Fact]
    public async Task RateMatch_ExtractedRateAgainstBundleTasa_Matches()
    {
        var ct = TestContext.Current.CancellationToken;
        var ps = await GetJulAgoPeriodSummaryAsync(ct);

        // Fixture #1 must have an extracted TASA; if not, the fixture or extractor is broken.
        ps.Tasa.Status.ShouldBe(ExtractionStatus.Extracted,
            "Fixture #1 must yield an extracted TASA so the Matched path is actually exercised");

        // TASA was extracted — build a bundle with the same rate so it matches.
        var extractedRate = ps.Tasa.Value!;
        var bundle = BuildBundleWithTasa(
            productId: "TC-BSSB",
            periodLabel: "Jul Ago 2025",
            periodStart: "2025-07-05",
            periodEnd: "2025-08-04",
            rate: extractedRate);

        var result = TasaMatcher.Match(ps, bundle, "TC-BSSB");

        result.Outcome.ShouldBe(TasaMatchOutcome.Matched,
            $"Extracted rate {extractedRate:P4} must match bundle rate {extractedRate:P4}");
        result.IsMatch.ShouldBeTrue();
        result.ExtractedRate.ShouldBe(extractedRate);
        result.BundleRate.ShouldBe(extractedRate);
        result.AbsoluteDifference.ShouldBe(0m);
        result.MatchedPeriodLabel.ShouldBe("Jul Ago 2025");
    }

    // -----------------------------------------------------------------------
    // Test S3.2-5a-missing: Rate match — TASA field not extracted → ExtractedTasaMissing
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the PeriodSummary carries a Missing TASA field, TasaMatcher must return
    /// <see cref="TasaMatchOutcome.ExtractedTasaMissing"/> regardless of what the bundle says.
    /// Uses a synthetic PeriodSummary so the test is independent of any fixture PDF.
    /// </summary>
    [Fact]
    public void RateMatch_ExtractedTasaMissing_ReturnsInsufficientData()
    {
        var missing = FieldLocator.PageHint(1);
        var ps = new PeriodSummary(
            product: ExtractedField<string>.Missing(missing),
            periodStart: ExtractedField<DateOnly>.Missing(missing),
            periodCutDate: ExtractedField<DateOnly>.Missing(missing),
            paymentDueDate: ExtractedField<DateOnly>.Missing(missing),
            dayCountPrinted: ExtractedField<int>.Missing(missing),
            dayCount: DayCountVerification.Compute(
                ExtractedField<DateOnly>.Missing(missing),
                ExtractedField<DateOnly>.Missing(missing),
                ExtractedField<int>.Missing(missing)),
            pagoParaNoGenerarIntereses: ExtractedField<decimal>.Missing(missing),
            pagoMinimo: ExtractedField<decimal>.Missing(missing),
            pagoMinimoMasMeses: ExtractedField<decimal>.Missing(missing),
            tasa: ExtractedField<decimal>.Missing(missing),   // ← Missing TASA
            cat: ExtractedField<decimal>.Missing(missing),
            saldoDeudorTotal: ExtractedField<decimal>.Missing(missing),
            creditoDisponible: ExtractedField<decimal>.Missing(missing));

        var bundle = BuildBundleWithTasa(
            productId: "TC-BSSB",
            periodLabel: "Jul Ago 2025",
            periodStart: "2025-07-05",
            periodEnd: "2025-08-04",
            rate: 0.1975m);

        var result = TasaMatcher.Match(ps, bundle, "TC-BSSB");

        result.Outcome.ShouldBe(TasaMatchOutcome.ExtractedTasaMissing,
            "When TASA is not extracted, outcome must be ExtractedTasaMissing");
        result.IsInsufficientData.ShouldBeTrue(
            "ExtractedTasaMissing must satisfy IsInsufficientData");
        result.ExtractedRate.ShouldBeNull("No extracted rate is available");
        result.BundleRate.ShouldBeNull("Bundle rate is not looked up when extracted TASA is missing");
        result.Detail.ShouldNotBeNullOrWhiteSpace("Detail must explain why the match failed");
    }

    // -----------------------------------------------------------------------
    // Test S3.2-5b: Rate match — bundle missing product/period → InsufficientData
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the bundle contains no rate entry for the product, TasaMatcher must
    /// return <see cref="TasaMatchOutcome.InsufficientData"/>.
    /// This covers the "bundle lacks that product/period" requirement (FR-5).
    /// </summary>
    [Fact]
    public async Task RateMatch_BundleMissingProductPeriod_ReturnsInsufficientData()
    {
        var ct = TestContext.Current.CancellationToken;

        // We need a PeriodSummary with an *extracted* TASA so TasaMatcher reaches
        // the bundle-lookup code (it short-circuits to ExtractedTasaMissing when
        // the field is NotExtracted, which is a different test scenario).
        // Build a synthetic PeriodSummary with TASA = 0.2736m (fixture rate).
        var tasaLocator = FieldLocator.PageHint(1);
        var synthTasa = ExtractedField<decimal>.Found(0.2736m, tasaLocator);

        // Minimal PeriodSummary — only the Tasa field matters for TasaMatcher.
        var missing = FieldLocator.PageHint(1);
        var ps = new PeriodSummary(
            product: ExtractedField<string>.Missing(missing),
            periodStart: ExtractedField<DateOnly>.Missing(missing),
            periodCutDate: ExtractedField<DateOnly>.Missing(missing),
            paymentDueDate: ExtractedField<DateOnly>.Missing(missing),
            dayCountPrinted: ExtractedField<int>.Missing(missing),
            dayCount: DayCountVerification.Compute(
                ExtractedField<DateOnly>.Missing(missing),
                ExtractedField<DateOnly>.Missing(missing),
                ExtractedField<int>.Missing(missing)),
            pagoParaNoGenerarIntereses: ExtractedField<decimal>.Missing(missing),
            pagoMinimo: ExtractedField<decimal>.Missing(missing),
            pagoMinimoMasMeses: ExtractedField<decimal>.Missing(missing),
            tasa: synthTasa,
            cat: ExtractedField<decimal>.Missing(missing),
            saldoDeudorTotal: ExtractedField<decimal>.Missing(missing),
            creditoDisponible: ExtractedField<decimal>.Missing(missing));

        _ = ct; // synthetic path — no fixture needed but keep ct in scope

        // Build a bundle for a completely different product.
        var bundle = BuildBundleWithTasa(
            productId: "TC-OTHER",
            periodLabel: "Jul Ago 2025",
            periodStart: "2025-07-05",
            periodEnd: "2025-08-04",
            rate: 0.2000m);

        // Looking up "TC-BSSB" — not present in the bundle.
        var result = TasaMatcher.Match(ps, bundle, "TC-BSSB");

        result.Outcome.ShouldBe(TasaMatchOutcome.InsufficientData,
            "When product is absent from bundle rate table, outcome must be InsufficientData");
        result.IsInsufficientData.ShouldBeTrue();
        result.BundleRate.ShouldBeNull("No bundle rate should be present");
        result.Detail.ShouldNotBeNullOrWhiteSpace();
    }

    // -----------------------------------------------------------------------
    // Test S3.2-6: Missing / absent summary field → NotExtracted with locator
    // -----------------------------------------------------------------------

    /// <summary>
    /// Every field in PeriodSummary must carry a non-null Locator and a non-negative
    /// Confidence, even when NotExtracted.  No field is silently blank.
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_AllPeriodFieldsHaveLocatorAndConfidence()
    {
        var ct = TestContext.Current.CancellationToken;
        var ps = await GetJulAgoPeriodSummaryAsync(ct);

        AssertPeriodField(ps.Product, nameof(ps.Product));
        AssertPeriodField(ps.PeriodStart, nameof(ps.PeriodStart));
        AssertPeriodField(ps.PeriodCutDate, nameof(ps.PeriodCutDate));
        AssertPeriodField(ps.PaymentDueDate, nameof(ps.PaymentDueDate));
        AssertPeriodField(ps.DayCountPrinted, nameof(ps.DayCountPrinted));
        AssertPeriodField(ps.PagoParaNoGenerarIntereses, nameof(ps.PagoParaNoGenerarIntereses));
        AssertPeriodField(ps.PagoMinimo, nameof(ps.PagoMinimo));
        AssertPeriodField(ps.PagoMinimoMasMeses, nameof(ps.PagoMinimoMasMeses));
        AssertPeriodField(ps.Tasa, nameof(ps.Tasa));
        AssertPeriodField(ps.Cat, nameof(ps.Cat));
        AssertPeriodField(ps.SaldoDeudorTotal, nameof(ps.SaldoDeudorTotal));
        AssertPeriodField(ps.CreditoDisponible, nameof(ps.CreditoDisponible));
    }

    // -----------------------------------------------------------------------
    // Test S3.2-7: ExtractFullAsync honours cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFull_Cancelled_ReturnsCancelled()
    {
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await extractor.ExtractFullAsync(pdf, cts.Token);

        result.IsFailure.ShouldBeTrue("Pre-cancelled token must produce a failure result");
        result.IsCancelled().ShouldBeTrue("Pre-cancelled token must produce a cancelled result");
    }

    // -----------------------------------------------------------------------
    // Test S3.2-8: ExtractFullAsync on empty bytes returns failure
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFull_EmptyBytes_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();

        var result = await extractor.ExtractFullAsync([], ct);

        result.IsFailure.ShouldBeTrue("Empty PDF bytes must produce a failure result");
        result.IsCancelled().ShouldBeFalse("Empty PDF is not a cancellation");
    }

    // -----------------------------------------------------------------------
    // Test S3.2-9: ExtractFullAsync header fields still populated (regression)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Ensures that ExtractFullAsync still produces correct header fields
    /// (regression check that adding PeriodSummary did not break header extraction).
    /// </summary>
    [Fact]
    public async Task ExtractFull_DummieVec_HeaderFieldsStillPopulated()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);
        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync must succeed. Error: {result.Error}");
        var model = result.Value!;

        // Core header fields must still be extracted.
        model.CardNumber.Status.ShouldBe(ExtractionStatus.Extracted);
        model.CardNumber.Value.ShouldBe("4567890123456789");
        model.Rfc.Status.ShouldBe(ExtractionStatus.Extracted);
        model.Rfc.Value.ShouldBe("PEPG780620225");
        model.BranchNumber.Value.ShouldBe("910");
        model.ClientNumber.Value.ShouldBe("99887766");

        // PeriodSummary must now be non-null.
        model.PeriodSummary.ShouldNotBeNull("ExtractFullAsync must populate PeriodSummary");
    }

    // -----------------------------------------------------------------------
    // Test S3.2-10: TasaMatcher pure-function — explicit mismatch
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the extracted rate is 19.75% but the bundle rate is 22.00%,
    /// TasaMatcher must return Mismatch.
    /// </summary>
    [Fact]
    public async Task RateMatch_BundleRateDiffers_ReturnsMismatch()
    {
        var ct = TestContext.Current.CancellationToken;
        var ps = await GetJulAgoPeriodSummaryAsync(ct);

        if (ps.Tasa.Status != ExtractionStatus.Extracted)
        {
            // TASA not in statement — this path tested elsewhere; pass trivially.
            return;
        }

        // Build a bundle with a deliberately different rate.
        var extractedRate = ps.Tasa.Value!;
        var differentRate = extractedRate + 0.05m; // 5 percentage-point difference

        var bundle = BuildBundleWithTasa("TC-BSSB", "Jul Ago 2025",
            "2025-07-05", "2025-08-04", differentRate);

        var result = TasaMatcher.Match(ps, bundle, "TC-BSSB");

        result.Outcome.ShouldBe(TasaMatchOutcome.Mismatch,
            $"Extracted {extractedRate:P4} vs bundle {differentRate:P4} (diff 0.05) must be Mismatch");
        result.IsMatch.ShouldBeFalse();
        result.AbsoluteDifference.ShouldBe(0.05m);
    }

    // -----------------------------------------------------------------------
    // Test S3.2-11: Real-parse on a PDF with no VEC labels → NotExtracted with page hint
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal in-memory PDF (single page, unrelated text) using
    /// <see cref="UglyToad.PdfPig.Writer.PdfDocumentBuilder"/>, then runs the real
    /// <see cref="PdfPigStatementFieldExtractor"/> over it.
    /// Asserts that every header identity field is reported as
    /// <see cref="ExtractionStatus.NotExtracted"/> with a non-null page-hint Locator
    /// that has no bounding box — proving the extractor degrades honestly on absent fields
    /// rather than throwing or returning null/blank values.
    /// </summary>
    [Fact]
    public async Task ExtractHeader_MinimalPdfWithNoLabeledFields_HeaderFieldsAreNotExtracted()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();

        // Build a single-page PDF whose text contains no VEC labels.
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842); // A4 in PDF points
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText("Hello", 12, new PdfPoint(50, 750), font);
        var pdfBytes = builder.Build();

        var result = await extractor.ExtractHeaderAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue(
            $"Extractor must succeed (not throw) on a valid PDF that lacks VEC labels. Error: {result.Error}");

        var model = result.Value!;

        // Every labeled header field must be NotExtracted with a page-hint locator.
        AssertNotExtractedWithPageHint(model.CardNumber, nameof(model.CardNumber));
        AssertNotExtractedWithPageHint(model.Clabe, nameof(model.Clabe));
        AssertNotExtractedWithPageHint(model.ClientNumber, nameof(model.ClientNumber));
        AssertNotExtractedWithPageHint(model.Rfc, nameof(model.Rfc));
        AssertNotExtractedWithPageHint(model.BranchNumber, nameof(model.BranchNumber));
    }

    /// <summary>
    /// Asserts that <paramref name="field"/> has <see cref="ExtractionStatus.NotExtracted"/>,
    /// a null value, a non-null page-hint locator on page 1, and no bounding box.
    /// </summary>
    private static void AssertNotExtractedWithPageHint<T>(ExtractedField<T> field, string name)
    {
        field.Status.ShouldBe(ExtractionStatus.NotExtracted,
            $"{name} must be NotExtracted when no VEC labels are present in the PDF");
        field.Value.ShouldBe(default,
            $"{name}.Value must be null/default when NotExtracted");
        field.Locator.ShouldNotBeNull(
            $"{name}.Locator must not be null — missing fields always carry a page hint");
        field.Locator.PageNumber.ShouldBe(1,
            $"{name}.Locator.PageNumber must be 1 (page-level hint)");
        field.Locator.HasBoundingBox.ShouldBeFalse(
            $"{name}.Locator must NOT have a bounding box when the field was not found");
    }

    // -----------------------------------------------------------------------
    // Assertion helpers
    // -----------------------------------------------------------------------

    private static void AssertPeriodField<T>(ExtractedField<T> field, string name)
    {
        field.ShouldNotBeNull($"{name} field must not be null");
        field.Locator.ShouldNotBeNull($"{name}.Locator must not be null (NotExtracted still needs hint)");
        field.Locator.PageNumber.ShouldBeGreaterThan(0, $"{name}.Locator.PageNumber must be >= 1");
        field.Confidence.ShouldBeGreaterThanOrEqualTo(0.0, $"{name}.Confidence >= 0");
        field.Confidence.ShouldBeLessThanOrEqualTo(1.0, $"{name}.Confidence <= 1");
    }

    // -----------------------------------------------------------------------
    // Bundle factory helper
    // -----------------------------------------------------------------------

    private static VecReferenceBundle BuildBundleWithTasa(
        string productId,
        string periodLabel,
        string periodStart,
        string periodEnd,
        decimal rate)
    {
        var metadata = new BundleMetadata("1.0.0", "Demo Bank", null, null, null, null);

        var product = new VecProduct(
            ProductId: productId,
            ProductName: $"Tarjeta de Crédito {productId}",
            Aliases: null,
            HasRewardsProgram: false,
            CardImage: null,
            ImportantMessageImage: null,
            Tariffs: null);

        var rateEntry = new InterestRateEntry(
            ProductId: productId,
            RatesByPeriod:
            [
                new RateByPeriod(
                    AnnualOrdinaryFixedRate: rate,
                    PeriodLabel: periodLabel,
                    PeriodStart: periodStart,
                    PeriodEnd: periodEnd)
            ]);

        return new VecReferenceBundle(
            BundleMetadata: metadata,
            Products: [product],
            InterestRates: [rateEntry],
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);
    }
}
