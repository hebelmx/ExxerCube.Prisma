using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting.DependencyInjection;
using IndQuestResults.Operations;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SkiaSharp;
using UglyToad.PdfPig.Writer;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Reporting.Tests;

/// <summary>
/// Unit and integration tests for <see cref="MarkedPdfGenerator"/> (Story 7.2, FR-16, CL-55).
/// </summary>
/// <remarks>
/// Input PDFs are built entirely in-memory with PdfSharp to avoid any fixture-file dependency.
/// Pixel-level highlight verification uses PDFtoImage (Conversion.ToImage → SKBitmap).
/// </remarks>
public sealed class MarkedPdfGeneratorTests
{
    // -----------------------------------------------------------------------
    // Fixtures / helpers
    // -----------------------------------------------------------------------

    private readonly IMarkedPdfGenerator _generator;

    public MarkedPdfGeneratorTests()
    {
        var logger = XUnitLogger.CreateLogger<MarkedPdfGenerator>(TestContext.Current.TestOutputHelper);
        _generator = new MarkedPdfGenerator(logger);
    }

    /// <summary>
    /// Builds a minimal multi-page PDF using PdfPig's <see cref="PdfDocumentBuilder"/>.
    /// Uses a Standard-14 font (Helvetica) so no external font resolver is required.
    /// Each page is A4 (595 × 842 pt) with a single line of text.
    /// </summary>
    private static byte[] BuildTestPdf(int pageCount = 1)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);

        for (var i = 0; i < pageCount; i++)
        {
            var page = builder.AddPage(595, 842); // A4 in PDF points
            // PdfPig origin is bottom-left; draw text near the top.
            page.AddText($"Test page {i + 1}", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 790), font);
        }

        return builder.Build();
    }

    /// <summary>Creates a FAIL finding with a precise bounding box on page 1.</summary>
    private static RuleFinding FailFindingWithBox(
        string checkId = "CL-TEST",
        int pageNumber = 1,
        double left = 100,
        double bottom = 400,
        double width = 200,
        double height = 30) =>
        RuleFinding.Fail(
            checkId: checkId,
            technique: TechniqueClass.Deterministic,
            severity: FindingSeverity.Critical,
            engineVersion: "1.0.0",
            expected: "expected",
            observed: "observed",
            locator: new FieldLocator(pageNumber, left, bottom, width, height));

    /// <summary>Creates a FAIL finding with a page-hint locator (no bounding box).</summary>
    private static RuleFinding FailFindingPageHint(string checkId = "CL-NOBB", int page = 1) =>
        RuleFinding.Fail(
            checkId: checkId,
            technique: TechniqueClass.Deterministic,
            severity: FindingSeverity.Warning,
            engineVersion: "1.0.0",
            locator: FieldLocator.PageHint(page));

    /// <summary>Creates a FAIL finding with a NoPage sentinel locator.</summary>
    private static RuleFinding FailFindingNoPage(string checkId = "CL-NOPAGE") =>
        RuleFinding.Fail(
            checkId: checkId,
            technique: TechniqueClass.Deterministic,
            severity: FindingSeverity.Warning,
            engineVersion: "1.0.0",
            locator: FieldLocator.NoPage());

    /// <summary>Creates a FAIL finding with null locator.</summary>
    private static RuleFinding FailFindingNullLocator(string checkId = "CL-NULL") =>
        RuleFinding.Fail(
            checkId: checkId,
            technique: TechniqueClass.Deterministic,
            severity: FindingSeverity.Warning,
            engineVersion: "1.0.0",
            locator: null);

    /// <summary>Creates a PASS finding — must never appear in output as a highlight.</summary>
    private static RuleFinding PassFinding(string checkId = "CL-PASS") =>
        RuleFinding.Pass(
            checkId: checkId,
            technique: TechniqueClass.Deterministic,
            engineVersion: "1.0.0",
            locator: new FieldLocator(1, 50, 750, 100, 20));

    // -----------------------------------------------------------------------
    // Test 1: FAIL with bounding box — success, non-empty, valid PDF, same page count
    // -----------------------------------------------------------------------

    /// <summary>
    /// A single FAIL finding with a full bounding box produces a valid, non-empty PDF with
    /// the same page count as the input.
    /// </summary>
    [Fact]
    public void Generate_FailFindingWithBoundingBox_ReturnSuccessValidPdfSamePageCount()
    {
        var inputPdf = BuildTestPdf(2);
        var findings = new List<RuleFinding> { FailFindingWithBox() };

        var result = _generator.Generate(inputPdf, findings, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("Generate must succeed for a valid PDF with FAIL findings.");
        result.Value.ShouldNotBeNull();
        result.Value!.Length.ShouldBeGreaterThan(0, "Output must be a non-empty byte array.");

        // Re-open the output and verify it is a valid PDF with the same page count.
        using var outputStream = new MemoryStream(result.Value);
        using var outputDoc = PdfReader.Open(outputStream, PdfDocumentOpenMode.Import);
        outputDoc.PageCount.ShouldBe(2, "Original page count must be preserved.");
    }

    // -----------------------------------------------------------------------
    // Test 2: Output differs from input (highlight was added)
    // -----------------------------------------------------------------------

    /// <summary>
    /// The output PDF bytes must differ from the input because a highlight rectangle was added.
    /// </summary>
    [Fact]
    public void Generate_FailFindingWithBoundingBox_OutputDiffersFromInput()
    {
        var inputPdf = BuildTestPdf(1);
        var findings = new List<RuleFinding> { FailFindingWithBox() };

        var result = _generator.Generate(inputPdf, findings, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.SequenceEqual(inputPdf).ShouldBeFalse(
            "The marked PDF must differ from the original because a highlight was drawn.");
    }

    // -----------------------------------------------------------------------
    // Test 3: Pixel-level highlight verification via PDFtoImage
    // -----------------------------------------------------------------------

    /// <summary>
    /// Renders the highlighted page with PDFtoImage and samples a pixel inside the drawn
    /// box.  The highlight colour is semi-transparent red drawn over a white page background,
    /// so the sampled pixel must have a noticeably higher red channel than green or blue.
    /// </summary>
    [Fact]
#pragma warning disable CA1416 // PDFtoImage is cross-platform (Windows, Linux, macOS)
    public void Generate_FailFindingWithBoundingBox_HighlightPixelIsRedTinted()
    {
        // 1-page A4 PDF; the box is near the vertical centre of the page.
        // PdfPig coords: left=100, bottom=400, width=200, height=30 (A4 = 595×842 pt).
        const double pdfPigLeft = 100;
        const double pdfPigBottom = 400;
        const double boxWidth = 200;
        const double boxHeight = 30;
        const double pageHeightPt = 842;

        var inputPdf = BuildTestPdf(1);
        var finding = FailFindingWithBox(left: pdfPigLeft, bottom: pdfPigBottom, width: boxWidth, height: boxHeight);

        var result = _generator.Generate(inputPdf, new[] { finding }, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();

        // Render at 150 DPI; A4 at 150 DPI → ~1240 × 1754 px.
        const int dpi = 150;
        const double ptPerInch = 72.0;
        double scale = dpi / ptPerInch;

        using var stream = new MemoryStream(result.Value!);
        using var bitmap = Conversion.ToImage(stream, leaveOpen: false, page: 0, options: new RenderOptions(Dpi: dpi));

        bitmap.ShouldNotBeNull("PDFtoImage must produce a non-null SKBitmap.");
        bitmap.Width.ShouldBeGreaterThan(0);
        bitmap.Height.ShouldBeGreaterThan(0);

        // PdfSharp top-left Y for the box:  pdfSharpTop = pageH - pdfPigBottom - boxH
        //   = 842 - 400 - 30 = 412 pt from the top (PdfSharp coords).
        // Sample the centre of the box.
        var sampleXPt = pdfPigLeft + boxWidth / 2.0;  // 200 pt from left
        var pdfSharpTopPt = pageHeightPt - pdfPigBottom - boxHeight; // 412 pt from top
        var sampleYPt = pdfSharpTopPt + boxHeight / 2.0;             // 427 pt from top

        // Convert to pixel coords.
        var sampleXPx = (int)(sampleXPt * scale);
        var sampleYPx = (int)(sampleYPt * scale);

        // Clamp within bitmap bounds.
        sampleXPx = Math.Clamp(sampleXPx, 0, bitmap.Width - 1);
        sampleYPx = Math.Clamp(sampleYPx, 0, bitmap.Height - 1);

        var pixel = bitmap.GetPixel(sampleXPx, sampleYPx);

        // The highlight is a semi-transparent red (R=220, G=50, B=50, A=90) blended onto white.
        // After alpha blending over white: R' ≈ 220*(90/255) + 255*(1-90/255) ≈ 178, G'≈B'≈236.
        // We simply assert R > G (red-dominant) which is the key invariant.
        pixel.Red.ShouldBeGreaterThan(
            pixel.Green,
            $"Sampled pixel at ({sampleXPx},{sampleYPx}) should be red-tinted: R={pixel.Red} G={pixel.Green} B={pixel.Blue}.");
    }
#pragma warning restore CA1416

    // -----------------------------------------------------------------------
    // Test 4: No FAIL findings — valid PDF, same page count, no error
    // -----------------------------------------------------------------------

    /// <summary>
    /// When there are no FAIL findings, Generate returns a valid copy of the original
    /// with the same page count and no error.
    /// </summary>
    [Fact]
    public void Generate_NoFailFindings_ReturnSuccessValidPdf()
    {
        var inputPdf = BuildTestPdf(2);
        var findings = new List<RuleFinding> { PassFinding() };

        var result = _generator.Generate(inputPdf, findings, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Length.ShouldBeGreaterThan(0);

        using var outputStream = new MemoryStream(result.Value);
        using var outputDoc = PdfReader.Open(outputStream, PdfDocumentOpenMode.Import);
        outputDoc.PageCount.ShouldBe(2, "Page count must be preserved even when no findings are marked.");
    }

    // -----------------------------------------------------------------------
    // Test 5: Empty findings list — valid PDF
    // -----------------------------------------------------------------------

    [Fact]
    public void Generate_EmptyFindingsList_ReturnSuccessValidPdf()
    {
        var inputPdf = BuildTestPdf(1);

        var result = _generator.Generate(inputPdf, new List<RuleFinding>(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();

        using var outputStream = new MemoryStream(result.Value!);
        using var outputDoc = PdfReader.Open(outputStream, PdfDocumentOpenMode.Import);
        outputDoc.PageCount.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Test 6: Finding with page-hint-only locator (no bounding box) — no crash
    // -----------------------------------------------------------------------

    /// <summary>
    /// A FAIL finding whose locator has a page number but no bounding box must not crash;
    /// the output must be a valid PDF.
    /// </summary>
    [Fact]
    public void Generate_FailFindingNoBoundingBox_NoCrashValidOutput()
    {
        var inputPdf = BuildTestPdf(2);
        var findings = new List<RuleFinding>
        {
            FailFindingPageHint(page: 1),
            FailFindingPageHint("CL-NOBB2", page: 2),
        };

        var result = _generator.Generate(inputPdf, findings, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("Page-hint-only findings must produce a valid result.");
        result.Value.ShouldNotBeNull();

        using var outputStream = new MemoryStream(result.Value!);
        using var outputDoc = PdfReader.Open(outputStream, PdfDocumentOpenMode.Import);
        outputDoc.PageCount.ShouldBe(2);
    }

    // -----------------------------------------------------------------------
    // Test 7: Finding with NoPage sentinel — silently skipped, valid output
    // -----------------------------------------------------------------------

    [Fact]
    public void Generate_FailFindingNoPageSentinel_SkippedValidOutput()
    {
        var inputPdf = BuildTestPdf(1);
        var findings = new List<RuleFinding> { FailFindingNoPage() };

        var result = _generator.Generate(inputPdf, findings, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("NoPage sentinel must be silently skipped.");
        result.Value.ShouldNotBeNull();

        using var outputStream = new MemoryStream(result.Value!);
        using var outputDoc = PdfReader.Open(outputStream, PdfDocumentOpenMode.Import);
        outputDoc.PageCount.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Test 8: Finding with null locator — silently skipped, valid output
    // -----------------------------------------------------------------------

    [Fact]
    public void Generate_FailFindingNullLocator_SkippedValidOutput()
    {
        var inputPdf = BuildTestPdf(1);
        var findings = new List<RuleFinding> { FailFindingNullLocator() };

        var result = _generator.Generate(inputPdf, findings, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("Null locator must be silently skipped.");
        result.Value.ShouldNotBeNull();

        using var outputStream = new MemoryStream(result.Value!);
        using var outputDoc = PdfReader.Open(outputStream, PdfDocumentOpenMode.Import);
        outputDoc.PageCount.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Test 9: Finding on out-of-range page — silently skipped, valid output
    // -----------------------------------------------------------------------

    [Fact]
    public void Generate_FailFindingOutOfRangePage_SkippedValidOutput()
    {
        var inputPdf = BuildTestPdf(1);
        // Page 5 does not exist in a 1-page PDF.
        var finding = FailFindingWithBox(pageNumber: 5);

        var result = _generator.Generate(inputPdf, new[] { finding }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("Out-of-range page must be silently skipped.");
        result.Value.ShouldNotBeNull();

        using var outputStream = new MemoryStream(result.Value!);
        using var outputDoc = PdfReader.Open(outputStream, PdfDocumentOpenMode.Import);
        outputDoc.PageCount.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Test 10: Corrupt / empty input — Result failure, no throw
    // -----------------------------------------------------------------------

    [Fact]
    public void Generate_EmptyInputBytes_ReturnFailureNoThrow()
    {
        var result = _generator.Generate(Array.Empty<byte>(), new List<RuleFinding>(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue("Empty byte array must yield a typed failure.");
        result.Error.ShouldNotBeNullOrEmpty("Failure must carry an error message.");
    }

    [Fact]
    public void Generate_CorruptBytes_ReturnFailureNoThrow()
    {
        var junk = "NOT A PDF"u8.ToArray();

        var result = _generator.Generate(junk, new List<RuleFinding>(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue("Corrupt bytes must yield a typed failure.");
        result.Error.ShouldNotBeNullOrEmpty();
    }

    // -----------------------------------------------------------------------
    // Test 11: Cancellation before work starts — Cancelled result
    // -----------------------------------------------------------------------

    [Fact]
    public void Generate_CancelledToken_ReturnCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var inputPdf = BuildTestPdf(1);
        var result = _generator.Generate(inputPdf, new List<RuleFinding>(), cts.Token);

        result.IsCancelled().ShouldBeTrue("Pre-cancelled token must produce a Cancelled result.");
    }

    // -----------------------------------------------------------------------
    // Test 12: Mixed findings — only FAIL with bbox receive a highlight
    // -----------------------------------------------------------------------

    /// <summary>
    /// When findings include a mix of PASS, FAIL-no-bbox, and FAIL-with-bbox, the output
    /// is still a valid PDF.  Only FAIL-with-bbox entries produce coordinate changes; the
    /// others are silently handled.
    /// </summary>
    [Fact]
    public void Generate_MixedFindings_ValidOutputNoThrow()
    {
        var inputPdf = BuildTestPdf(2);
        var findings = new List<RuleFinding>
        {
            PassFinding("CL-OK"),
            FailFindingPageHint("CL-P1", 1),
            FailFindingWithBox("CL-BOX", pageNumber: 1),
            FailFindingNoPage("CL-NP"),
            FailFindingNullLocator("CL-NULL2"),
            FailFindingWithBox("CL-BOX2", pageNumber: 2, left: 50, bottom: 200, width: 100, height: 15),
        };

        var result = _generator.Generate(inputPdf, findings, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("Mixed findings must not throw or fail.");
        result.Value.ShouldNotBeNull();

        using var outputStream = new MemoryStream(result.Value!);
        using var outputDoc = PdfReader.Open(outputStream, PdfDocumentOpenMode.Import);
        outputDoc.PageCount.ShouldBe(2);
    }

    // -----------------------------------------------------------------------
    // Test 13: DI wiring — AddVeriqanReporting registers IMarkedPdfGenerator
    // -----------------------------------------------------------------------

    [Fact]
    public void AddVeriqanReporting_RegistersIMarkedPdfGenerator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanReporting();

        using var sp = services.BuildServiceProvider();

        var generator = sp.GetService<IMarkedPdfGenerator>();
        generator.ShouldNotBeNull("AddVeriqanReporting must register IMarkedPdfGenerator.");
        generator.ShouldBeOfType<MarkedPdfGenerator>();
    }
}
