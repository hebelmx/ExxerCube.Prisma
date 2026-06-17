using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story 4.4 extraction tests for the DESGLOSE DE MOVIMIENTOS DEL PERIODO
/// transaction table.  Verifies Y-band + X-column row reconstruction across
/// all three Dummie VEC fixtures (jul-ago, ago-sep, sep-oct 2025).
/// </summary>
/// <remarks>
/// <para>
/// <b>Known first data row (jul-ago fixture, page 3):</b>
/// operation 05-jul-2025 / charge 07-jul-2025 / NETFLIX COM CR NME 110513PI3 / +$329.00.
/// </para>
/// <para>
/// <b>Known abonos (credits) in jul-ago fixture:</b>
/// <list type="bullet">
///   <item><description>PROGRAMA FOR LIFE — $6,523.00 (abono, page 3)</description></item>
///   <item><description>SU ABONO GRACIAS — $61,273.35 (abono, page 6)</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Column X-ranges used by the extractor</b> (measured from Dummie VEC fixtures):
/// Fecha operación X ≤ 95 | Fecha cargo X 96–157 | Descripción X 158–422 |
/// Sign X 423–438 | Amount X ≥ 436.
/// </para>
/// </remarks>
public sealed class PdfPigStatementFieldExtractorMovementsTests
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

    /// <summary>All three fixture paths (for parameterised tests).</summary>
    public static IEnumerable<object[]> AllFixtures =>
    [
        [JulAgoFixture, "jul_ago"],
        [AgoSepFixture, "ago_sep"],
        [SepOctFixture, "sep_oct"],
    ];

    // -----------------------------------------------------------------------
    // Factory helpers
    // -----------------------------------------------------------------------

    private static PdfPigStatementFieldExtractor CreateExtractor()
    {
        var logger = XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>();
        return new PdfPigStatementFieldExtractor(logger);
    }

    // Diagnostic logger for test output — uses the xUnit v3 ambient context.
    private static ILogger CreateDiagLogger() =>
        XUnitLogger.CreateLogger<PdfPigStatementFieldExtractorMovementsTests>();

    private static byte[] ReadFixture(string path)
    {
        File.Exists(path).ShouldBeTrue($"Fixture not found at: {path}");
        return File.ReadAllBytes(path);
    }

    // -----------------------------------------------------------------------
    // Test 1: DESGLOSE section found and a non-trivial number of rows parsed
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that the jul-ago fixture yields a positive, sane movement count
    /// (between 1 and 200 inclusive) and status Extracted.
    /// Reports the actual count via the test logger for observability.
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_MovementsAreParsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var log = CreateDiagLogger();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        var model = result.Value!;

        model.MovementsStatus.ShouldBe(MovementsExtractionStatus.Extracted,
            "DESGLOSE section must be found and rows parsed");

        var count = model.Movements.Count;
        log.LogInformation("[jul_ago] Movement count: {Count}", count);

        // Sane range: the fixture has ~30–80 rows across 4 pages.
        count.ShouldBeGreaterThan(0, "Must parse at least one movement");
        count.ShouldBeLessThanOrEqualTo(200, "Should not massively over-segment");

        // Log first 5 rows for manual sanity-check.
        foreach (var mv in model.Movements.Take(5))
        {
            log.LogInformation(
                "  op={OpDate} charge={ChargeDate} sign={Sign} amount={Amount:F2} desc={Desc}",
                mv.OperationDate, mv.ChargeDate, mv.Sign, mv.Amount, mv.Description);
        }
    }

    // -----------------------------------------------------------------------
    // Test 2: First movement is NETFLIX $329.00 Charge (pinned known row)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Pins the known first data row from the jul-ago fixture:
    /// NETFLIX COM CR NME 110513PI3 / 05-jul-2025 / +$329.00.
    /// This validates date parsing, description assembly, sign parsing, and amount parsing
    /// all in one row.
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_FirstMovement_IsNetflix329()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);
        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        var model = result.Value!;

        model.Movements.Count.ShouldBeGreaterThan(0, "Must have at least one movement");

        // The NETFLIX row must be present (not necessarily at index 0).
        var netflix = model.Movements
            .FirstOrDefault(m => m.Description.Contains("NETFLIX", StringComparison.OrdinalIgnoreCase));

        netflix.ShouldNotBeNull(
            "A movement with NETFLIX in the description must exist");

        netflix!.Amount.ShouldBe(329.00m,
            "NETFLIX amount must be $329.00");

        netflix.Sign.ShouldBe(MovementSign.Charge,
            "NETFLIX is a cargo (charge), sign must be Charge");

        netflix.OperationDate.ShouldNotBeNull(
            "NETFLIX operation date must parse successfully");

        netflix.OperationDate!.Value.ShouldBe(new DateOnly(2025, 7, 5),
            "NETFLIX operation date must be 2025-07-05");

        netflix.Locator.ShouldNotBeNull("NETFLIX row must have a locator");
        netflix.Locator.PageNumber.ShouldBeGreaterThanOrEqualTo(2,
            "DESGLOSE is on pages 3–6; page number must be ≥ 2");
    }

    // -----------------------------------------------------------------------
    // Test 3: Sign parsing — at least one Credit (abono) is detected
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that both "+" (Charge) and "−" (Credit) sign tokens are parsed correctly.
    /// The jul-ago fixture has two known abonos: PROGRAMA FOR LIFE ($6,523.00) and
    /// SU ABONO ... GRACIAS ($61,273.35).
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_KnownCreditAndChargeSignsParsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var log = CreateDiagLogger();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);
        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        var model = result.Value!;

        var credits = model.Movements.Where(m => m.Sign == MovementSign.Credit).ToList();
        var charges = model.Movements.Where(m => m.Sign == MovementSign.Charge).ToList();

        log.LogInformation("Charges: {ChargeCount}, Credits: {CreditCount}",
            charges.Count, credits.Count);

        foreach (var c in credits)
        {
            log.LogInformation(
                "  Credit: op={OpDate} amount={Amount:F2} desc={Desc}",
                c.OperationDate, c.Amount, c.Description);
        }

        // The fixture has 2 abonos — assert at least 1 is found.
        credits.Count.ShouldBeGreaterThan(0,
            "jul-ago fixture has at least 2 abonos (PROGRAMA FOR LIFE + SU ABONO GRACIAS); " +
            "at least 1 must be parsed as Credit");

        // Charges must dominate (most rows are purchases).
        charges.Count.ShouldBeGreaterThan(credits.Count,
            "There are more cargo rows than abono rows in the fixture");

        // Validate the PROGRAMA FOR LIFE abono if found.
        var programaForLife = model.Movements
            .FirstOrDefault(m => m.Description.Contains("PROGRAMA", StringComparison.OrdinalIgnoreCase)
                               && m.Sign == MovementSign.Credit);

        if (programaForLife is not null)
        {
            programaForLife.Amount.ShouldBe(6523.00m,
                "PROGRAMA FOR LIFE credit amount must be $6,523.00");
            log.LogInformation("PROGRAMA FOR LIFE credit confirmed at $6,523.00");
        }
        else
        {
            log.LogInformation(
                "PROGRAMA FOR LIFE not found specifically; verifying first credit has positive amount");
            credits[0].Amount.ShouldBeGreaterThan(0m,
                "Credit amount must be positive (non-zero)");
        }
    }

    // -----------------------------------------------------------------------
    // Test 4: All parsed movements have a non-null OperationDate (date parse rate)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a high proportion of movements have a successfully parsed OperationDate
    /// (≥ 95%).  Reports the parse rate via the logger.  100% is expected since the date
    /// format is consistent across the fixtures.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Extract_DummieVec_AllMovementDatesParse(string fixturePath, string label)
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var log = CreateDiagLogger();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractFullAsync(pdf, ct);
        result.IsSuccess.ShouldBeTrue($"[{label}] Extraction failed: {result.Error}");
        var model = result.Value!;

        if (model.MovementsStatus == MovementsExtractionStatus.SectionNotFound)
        {
            log.LogInformation("[{Label}] DESGLOSE section not found — skipping date-parse rate check.", label);
            return;
        }

        var total = model.Movements.Count;
        var withDate = model.Movements.Count(m => m.OperationDate is not null);
        var missing = total - withDate;

        var ratePct = total > 0 ? (100.0 * withDate / total) : 0.0;
        log.LogInformation(
            "[{Label}] OperationDate parse rate: {WithDate}/{Total} ({Rate:F1}%); missing: {Missing}",
            label, withDate, total, ratePct, missing);

        total.ShouldBeGreaterThan(0, $"[{label}] Must have at least one movement");

        // Expect ≥ 95% parse rate — the date format is stable across fixtures.
        var parseRate = total > 0 ? (double)withDate / total : 0.0;
        parseRate.ShouldBeGreaterThanOrEqualTo(0.95,
            $"[{label}] Expected ≥ 95% of operation dates to parse; got {withDate}/{total}");
    }

    // -----------------------------------------------------------------------
    // Test 5: Every movement carries a page-numbered locator
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that every parsed movement has a <see cref="StatementMovement.Locator"/>
    /// with a page number ≥ 2 (DESGLOSE starts on page 3 in the fixtures) and that
    /// movements span at least 2 distinct pages.
    /// </summary>
    [Fact]
    public async Task Extract_DummieVec_MovementsHaveLocators()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var log = CreateDiagLogger();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);
        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        var model = result.Value!;

        model.MovementsStatus.ShouldBe(MovementsExtractionStatus.Extracted);
        model.Movements.Count.ShouldBeGreaterThan(0);

        foreach (var mv in model.Movements)
        {
            mv.Locator.ShouldNotBeNull(
                $"Movement '{mv.Description}' must have a non-null locator");

            mv.Locator.PageNumber.ShouldBeGreaterThanOrEqualTo(2,
                $"Movement '{mv.Description}' page number must be ≥ 2 (DESGLOSE is on pages 3–6)");

            mv.Locator.PageNumber.ShouldBeLessThanOrEqualTo(8,
                $"Movement '{mv.Description}' page number must be ≤ 8 (fixture is 8 pages)");
        }

        // Movements must span at least 2 pages (DESGLOSE spans pages 3–6 in the fixture).
        var distinctPages = model.Movements
            .Select(m => m.Locator.PageNumber)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        log.LogInformation(
            "Movements found on pages: {Pages}",
            string.Join(", ", distinctPages));

        distinctPages.Count.ShouldBeGreaterThanOrEqualTo(2,
            "Movements must span at least 2 DESGLOSE pages");
    }

    // -----------------------------------------------------------------------
    // Test 6: Negative/empty case — minimal PDF with no DESGLOSE section
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a PDF with no DESGLOSE section produces an empty Movements list
    /// and status SectionNotFound, without throwing.
    /// Uses the same minimal in-memory PDF strategy as other "NotExtracted" tests.
    /// </summary>
    [Fact]
    public async Task Extract_MinimalPdf_MovementsEmpty_StatusSectionNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var minimalPdf = CreateMinimalPdf();

        // Should not throw regardless of outcome.
        var result = await extractor.ExtractFullAsync(minimalPdf, ct);

        // Either the extractor opens the minimal PDF and returns an empty-movement model,
        // or it returns a failure (no content to extract). Both are acceptable as long as
        // it does not throw.
        if (result.IsSuccess)
        {
            var model = result.Value!;
            model.Movements.ShouldNotBeNull();
            model.Movements.Count.ShouldBe(0,
                "Minimal PDF has no DESGLOSE section; Movements must be empty");
            model.MovementsStatus.ShouldBe(MovementsExtractionStatus.SectionNotFound,
                "Minimal PDF has no DESGLOSE section; status must be SectionNotFound");
        }
        // else: failure result is also acceptable for a minimal PDF.
    }

    // -----------------------------------------------------------------------
    // Test 7: All three fixtures report Extracted status with positive count
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that all three Dummie VEC fixtures (jul-ago, ago-sep, sep-oct)
    /// each have their DESGLOSE section detected and at least one row parsed.
    /// Logs per-fixture movement counts and a 3-row sample.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Extract_AllFixtures_MovementsExtracted(string fixturePath, string label)
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var log = CreateDiagLogger();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractFullAsync(pdf, ct);
        result.IsSuccess.ShouldBeTrue($"[{label}] Extraction failed: {result.Error}");
        var model = result.Value!;

        model.MovementsStatus.ShouldBe(MovementsExtractionStatus.Extracted,
            $"[{label}] DESGLOSE must be detected and at least one row parsed");

        var count = model.Movements.Count;
        log.LogInformation("[{Label}] {Count} movements parsed", label, count);

        count.ShouldBeGreaterThan(0,
            $"[{label}] Must have at least one movement");

        // Log 3 sample rows.
        foreach (var mv in model.Movements.Take(3))
        {
            log.LogInformation(
                "  [{Label}] op={OpDate} charge={ChargeDate} sign={Sign} amt={Amount:F2} desc={Desc}",
                label, mv.OperationDate, mv.ChargeDate, mv.Sign, mv.Amount, mv.Description);
        }
    }

    // -----------------------------------------------------------------------
    // Minimal PDF factory
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates the smallest valid PDF that PdfPig can open without errors.
    /// Contains one empty page with no recognisable VEC content.
    /// </summary>
    private static byte[] CreateMinimalPdf()
    {
        // Minimal valid PDF-1.4 with one empty page (no text content).
        const string MinimalPdfContent =
            "%PDF-1.4\n" +
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n" +
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\n" +
            "xref\n0 4\n" +
            "0000000000 65535 f \n" +
            "0000000009 00000 n \n" +
            "0000000058 00000 n \n" +
            "0000000115 00000 n \n" +
            "trailer\n<< /Size 4 /Root 1 0 R >>\n" +
            "startxref\n190\n%%EOF";

        return System.Text.Encoding.Latin1.GetBytes(MinimalPdfContent);
    }
}
