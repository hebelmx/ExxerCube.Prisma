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

    // -----------------------------------------------------------------------
    // Tests 14–18: ComputeHighlightRect — coordinate transform per rotation
    //
    // Fixture geometry: A4 portrait MediaBox (595 × 842 pt), no CropBox.
    // Visual bounding box: left=100, bottom=200, width=50, height=30.
    //
    // Derivation is documented in MarkedPdfGenerator.ComputeHighlightRect XML doc.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Baseline: 0° rotation (no CropBox) produces the standard Y-flip and preserves
    /// visual width/height unchanged.
    /// </summary>
    [Fact]
    public void ComputeHighlightRect_Rotate0NoCropBox_StandardYFlip()
    {
        // A4 portrait, no CropBox (cx1=cy1=0, cw=595, ch=842).
        var rect = MarkedPdfGenerator.ComputeHighlightRect(
            rotateDegrees: 0,
            mediaBoxHeight: 842,
            cx1: 0, cy1: 0,
            cropWidth: 595, cropHeight: 842,
            vx: 100, vy: 200,
            vw: 50,  vh: 30);

        // rawX = 0 + 100 = 100
        rect.X.ShouldBe(100.0, "0°: rawX = cx1 + vx");

        // pdfSharpTop = 842 − 0 − 200 − 30 = 612
        rect.Y.ShouldBe(612.0, "0°: pdfSharpTop = mh − cy1 − vy − vh");

        // Dimensions unchanged at 0°.
        rect.Width.ShouldBe(50.0,  "0°: drawW = vw (no axis swap)");
        rect.Height.ShouldBe(30.0, "0°: drawH = vh (no axis swap)");
    }

    /// <summary>
    /// 90° CW viewer rotation: the viewer rotates the raw page 90° CW, so we invert (90° CCW)
    /// to recover raw coords.  rawX = cx1 + cropWidth − vy − vh; axes swap (drawW=vh, drawH=vw).
    /// </summary>
    [Fact]
    public void ComputeHighlightRect_Rotate90NoCropBox_AxesSwappedCorrectQuadrant()
    {
        var rect = MarkedPdfGenerator.ComputeHighlightRect(
            rotateDegrees: 90,
            mediaBoxHeight: 842,
            cx1: 0, cy1: 0,
            cropWidth: 595, cropHeight: 842,
            vx: 100, vy: 200,
            vw: 50,  vh: 30);

        // rawX = 0 + 595 − 200 − 30 = 365
        rect.X.ShouldBe(365.0, "90°: rawX = cx1 + cropWidth − vy − vh");

        // pdfSharpTop = 842 − 0 − 100 − 50 = 692
        rect.Y.ShouldBe(692.0, "90°: pdfSharpTop = mh − cy1 − vx − vw");

        // Axes swap: visual height (30) becomes raw width, visual width (50) becomes raw height.
        rect.Width.ShouldBe(30.0,  "90°: drawW = vh (axes swap)");
        rect.Height.ShouldBe(50.0, "90°: drawH = vw (axes swap)");

        // Sanity: the raw rect must lie inside the raw MediaBox (0..595 w, 0..842 h).
        rect.X.ShouldBeGreaterThanOrEqualTo(0);
        (rect.X + rect.Width).ShouldBeLessThanOrEqualTo(595);
        rect.Y.ShouldBeGreaterThanOrEqualTo(0);
        (rect.Y + rect.Height).ShouldBeLessThanOrEqualTo(842);
    }

    /// <summary>
    /// 180° viewer rotation: both axes invert, dimensions stay the same.
    /// </summary>
    [Fact]
    public void ComputeHighlightRect_Rotate180NoCropBox_BothAxesInvertedSameDimensions()
    {
        var rect = MarkedPdfGenerator.ComputeHighlightRect(
            rotateDegrees: 180,
            mediaBoxHeight: 842,
            cx1: 0, cy1: 0,
            cropWidth: 595, cropHeight: 842,
            vx: 100, vy: 200,
            vw: 50,  vh: 30);

        // rawX = 0 + 595 − 100 − 50 = 445
        rect.X.ShouldBe(445.0, "180°: rawX = cx1 + cropWidth − vx − vw");

        // pdfSharpTop = 842 − 0 − 842 + 200 = 200
        rect.Y.ShouldBe(200.0, "180°: pdfSharpTop = mh − cy1 − cropHeight + vy");

        // 180° does not swap axes — dimensions are unchanged.
        rect.Width.ShouldBe(50.0,  "180°: drawW = vw (no axis swap)");
        rect.Height.ShouldBe(30.0, "180°: drawH = vh (no axis swap)");

        // Sanity bounds check.
        rect.X.ShouldBeGreaterThanOrEqualTo(0);
        (rect.X + rect.Width).ShouldBeLessThanOrEqualTo(595);
        rect.Y.ShouldBeGreaterThanOrEqualTo(0);
        (rect.Y + rect.Height).ShouldBeLessThanOrEqualTo(842);
    }

    /// <summary>
    /// 270° CW (= 90° CCW) viewer rotation: the viewer rotates 270° CW, so we invert (90° CW)
    /// to recover raw coords.  rawX = cx1 + vy; pdfSharpTop = mh − cy1 − cropHeight + vx; axes swap.
    /// </summary>
    [Fact]
    public void ComputeHighlightRect_Rotate270NoCropBox_AxesSwappedCorrectQuadrant()
    {
        var rect = MarkedPdfGenerator.ComputeHighlightRect(
            rotateDegrees: 270,
            mediaBoxHeight: 842,
            cx1: 0, cy1: 0,
            cropWidth: 595, cropHeight: 842,
            vx: 100, vy: 200,
            vw: 50,  vh: 30);

        // rawX = 0 + 200 = 200
        rect.X.ShouldBe(200.0, "270°: rawX = cx1 + vy");

        // pdfSharpTop = 842 − 0 − 842 + 100 = 100
        rect.Y.ShouldBe(100.0, "270°: pdfSharpTop = mh − cy1 − cropHeight + vx");

        // Axes swap: visual height (30) → raw width, visual width (50) → raw height.
        rect.Width.ShouldBe(30.0,  "270°: drawW = vh (axes swap)");
        rect.Height.ShouldBe(50.0, "270°: drawH = vw (axes swap)");

        // Sanity bounds: raw MediaBox is 595 (width) × 842 (height).
        rect.X.ShouldBeGreaterThanOrEqualTo(0);
        (rect.X + rect.Width).ShouldBeLessThanOrEqualTo(595, "270°: raw X + drawW must not exceed MediaBox width (595)");
        rect.Y.ShouldBeGreaterThanOrEqualTo(0);
        (rect.Y + rect.Height).ShouldBeLessThanOrEqualTo(842);
    }

    /// <summary>
    /// CropBox offset (0° rotation): a non-origin CropBox shifts the raw origin, so the
    /// computed X and Y must be offset by (cx1, cy1) relative to the no-CropBox result.
    /// </summary>
    [Fact]
    public void ComputeHighlightRect_Rotate0WithCropBoxOffset_RawCoordsShiftedByCropOrigin()
    {
        // CropBox: X1=10, Y1=20, width=500, height=800 inside an 842-pt-tall MediaBox.
        var rect = MarkedPdfGenerator.ComputeHighlightRect(
            rotateDegrees: 0,
            mediaBoxHeight: 842,
            cx1: 10, cy1: 20,
            cropWidth: 500, cropHeight: 800,
            vx: 100, vy: 200,
            vw: 50,  vh: 30);

        // rawX = 10 + 100 = 110
        rect.X.ShouldBe(110.0, "0° + CropBox: rawX = cx1 + vx");

        // pdfSharpTop = 842 − 20 − 200 − 30 = 592
        rect.Y.ShouldBe(592.0, "0° + CropBox: pdfSharpTop = mh − cy1 − vy − vh");

        rect.Width.ShouldBe(50.0,  "0° + CropBox: drawW unchanged");
        rect.Height.ShouldBe(30.0, "0° + CropBox: drawH unchanged");
    }

    // -----------------------------------------------------------------------
    // Tests 19–22: ComputeHighlightRect bounds-invariant property test
    //
    // For every rotation value and several visual boxes (edges, corners, centre) the
    // computed XRect must lie fully inside the raw MediaBox.  This is the test that
    // would have caught the 270° cropHeight-vs-cropWidth dimensional defect.
    //
    // A4 portrait: raw MediaBox = 595 (W) × 842 (H).
    // /Rotate∈{0,180} → visual dims = 595 × 842; /Rotate∈{90,270} → visual dims = 842 × 595.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Provides (rotate, vx, vy, vw, vh) tuples: for each rotation the visual box is
    /// placed near each corner and the centre of the rotation-appropriate visual space.
    /// </summary>
    public static TheoryData<int, double, double, double, double> BoundsInvariantCases()
    {
        // A4 portrait MediaBox: raw width=595, raw height=842.
        // /Rotate∈{0,180}: visual width=595, visual height=842.
        // /Rotate∈{90,270}: visual width=842, visual height=595.
        var data = new TheoryData<int, double, double, double, double>();

        // box dimensions used in each test
        const double bw = 50;
        const double bh = 30;

        foreach (var rotate in new[] { 0, 90, 180, 270 })
        {
            // Visual page dimensions for this rotation.
            double vW = rotate is 90 or 270 ? 842.0 : 595.0; // visual width
            double vH = rotate is 90 or 270 ? 595.0 : 842.0; // visual height

            // Near each of the four corners and the centre.
            double[] xs = [1.0, vW - bw - 1.0, (vW - bw) / 2.0];
            double[] ys = [1.0, vH - bh - 1.0, (vH - bh) / 2.0];

            foreach (var vx in xs)
            {
                foreach (var vy in ys)
                {
                    data.Add(rotate, vx, vy, bw, bh);
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Property: for every rotation and every valid visual box, the computed XRect must lie
    /// fully inside the raw MediaBox (0 ≤ X, X+Width ≤ 595, 0 ≤ Y, Y+Height ≤ 842).
    /// </summary>
    [Theory]
    [MemberData(nameof(BoundsInvariantCases))]
    public void ComputeHighlightRect_AllRotations_RectLiesWithinRawMediaBox(
        int rotate, double vx, double vy, double vw, double vh)
    {
        // A4 portrait MediaBox, no CropBox.
        const double mediaBoxHeight = 842;
        const double cropWidth  = 595;
        const double cropHeight = 842;

        var rect = MarkedPdfGenerator.ComputeHighlightRect(
            rotateDegrees: rotate,
            mediaBoxHeight: mediaBoxHeight,
            cx1: 0, cy1: 0,
            cropWidth: cropWidth, cropHeight: cropHeight,
            vx: vx, vy: vy,
            vw: vw, vh: vh);

        rect.X.ShouldBeGreaterThanOrEqualTo(
            0,
            $"rotate={rotate} vx={vx} vy={vy}: X must be ≥ 0");
        (rect.X + rect.Width).ShouldBeLessThanOrEqualTo(
            cropWidth,
            $"rotate={rotate} vx={vx} vy={vy}: X+Width must be ≤ {cropWidth} (raw MediaBox width)");
        rect.Y.ShouldBeGreaterThanOrEqualTo(
            0,
            $"rotate={rotate} vx={vx} vy={vy}: Y must be ≥ 0");
        (rect.Y + rect.Height).ShouldBeLessThanOrEqualTo(
            mediaBoxHeight,
            $"rotate={rotate} vx={vx} vy={vy}: Y+Height must be ≤ {mediaBoxHeight} (raw MediaBox height)");
    }

    // -----------------------------------------------------------------------
    // Tests 23–26: end-to-end Generate with rotated PDF pages (PdfSharp-built)
    //
    // Uses PdfSharp to create PDFs with /Rotate set; verifies Generate succeeds
    // and output is a valid modified PDF (bytes differ from input).
    // -----------------------------------------------------------------------

    /// <summary>Builds a minimal 1-page PDF with PdfSharp and a specific /Rotate value.</summary>
    private static byte[] BuildRotatedPdfSharpPdf(int rotate)
    {
        using var doc = new PdfDocument();
        var page = doc.AddPage();
        // A4 portrait MediaBox (595 × 842 pt).
        page.Width  = PdfSharp.Drawing.XUnit.FromPoint(595);
        page.Height = PdfSharp.Drawing.XUnit.FromPoint(842);
        page.Rotate = rotate;

        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Generate_RotatedPage90_SucceedsValidPdfBytesModified()
    {
        var inputPdf = BuildRotatedPdfSharpPdf(90);
        // Visual box for a 90° page (visual dims = 842w × 595h):
        // place near the visual centre so coords are safely inside bounds.
        var finding = FailFindingWithBox(pageNumber: 1, left: 200, bottom: 100, width: 50, height: 30);

        var result = _generator.Generate(inputPdf, new[] { finding }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("Generate must succeed for a /Rotate=90 page.");
        result.Value.ShouldNotBeNull();
        result.Value!.Length.ShouldBeGreaterThan(0);
        result.Value.SequenceEqual(inputPdf).ShouldBeFalse(
            "Output must differ from input because a highlight was drawn.");

        using var outputStream = new MemoryStream(result.Value);
        using var outputDoc = PdfReader.Open(outputStream, PdfDocumentOpenMode.Import);
        outputDoc.PageCount.ShouldBe(1, "Page count must be preserved.");
    }

    [Fact]
    public void Generate_RotatedPage180_SucceedsValidPdfBytesModified()
    {
        var inputPdf = BuildRotatedPdfSharpPdf(180);
        var finding = FailFindingWithBox(pageNumber: 1, left: 100, bottom: 200, width: 50, height: 30);

        var result = _generator.Generate(inputPdf, new[] { finding }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("Generate must succeed for a /Rotate=180 page.");
        result.Value.ShouldNotBeNull();
        result.Value!.Length.ShouldBeGreaterThan(0);
        result.Value.SequenceEqual(inputPdf).ShouldBeFalse(
            "Output must differ from input because a highlight was drawn.");

        using var outputStream = new MemoryStream(result.Value);
        using var outputDoc = PdfReader.Open(outputStream, PdfDocumentOpenMode.Import);
        outputDoc.PageCount.ShouldBe(1, "Page count must be preserved.");
    }

    [Fact]
    public void Generate_RotatedPage270_SucceedsValidPdfBytesModified()
    {
        var inputPdf = BuildRotatedPdfSharpPdf(270);
        // Visual box for a 270° page (visual dims = 842w × 595h):
        var finding = FailFindingWithBox(pageNumber: 1, left: 200, bottom: 100, width: 50, height: 30);

        var result = _generator.Generate(inputPdf, new[] { finding }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("Generate must succeed for a /Rotate=270 page.");
        result.Value.ShouldNotBeNull();
        result.Value!.Length.ShouldBeGreaterThan(0);
        result.Value.SequenceEqual(inputPdf).ShouldBeFalse(
            "Output must differ from input because a highlight was drawn.");

        using var outputStream = new MemoryStream(result.Value);
        using var outputDoc = PdfReader.Open(outputStream, PdfDocumentOpenMode.Import);
        outputDoc.PageCount.ShouldBe(1, "Page count must be preserved.");
    }

    // -----------------------------------------------------------------------
    // Tests 24–29: tier-coded colour + numbered callouts (Story 2.1, Epic 2)
    // -----------------------------------------------------------------------

    /// <summary>
    /// A Bank-tier FAIL finding must render an amber highlight, NOT red.
    /// Amber fill RGB(255,193,7) at 90/255 alpha over white yields R≈255, G≈233, B≈168.
    /// Key invariant: G > B (amber's blue is suppressed; this distinguishes amber from red,
    /// which blends to R=243, G=183, B=183 — nearly equal G and B).
    /// </summary>
    [Fact]
#pragma warning disable CA1416
    public void Generate_BankTierCheckId_HighlightPixelIsAmberTinted()
    {
        const double pdfPigLeft = 100, pdfPigBottom = 400, boxWidth = 200, boxHeight = 30, pageH = 842;
        var inputPdf = BuildTestPdf(1);
        var finding  = FailFindingWithBox("CL-BANK", left: pdfPigLeft, bottom: pdfPigBottom, width: boxWidth, height: boxHeight);
        var tiers    = new Dictionary<string, ChecklistTier> { ["CL-BANK"] = ChecklistTier.Bank };

        var result = _generator.Generate(inputPdf, new[] { finding }, TestContext.Current.CancellationToken, tiers);
        result.IsSuccess.ShouldBeTrue("Generate must succeed with a Bank-tier map.");

        const int dpi = 150;
        const double ptPerInch = 72.0;
        double scale = dpi / ptPerInch;

        using var stream = new MemoryStream(result.Value!);
        using var bitmap = Conversion.ToImage(stream, leaveOpen: false, page: 0, options: new RenderOptions(Dpi: dpi));
        bitmap.ShouldNotBeNull();

        var sampleXPx = (int)((pdfPigLeft + boxWidth  / 2.0) * scale);
        var sampleYPx = (int)(((pageH - pdfPigBottom - boxHeight) + boxHeight / 2.0) * scale);
        sampleXPx = Math.Clamp(sampleXPx, 0, bitmap.Width  - 1);
        sampleYPx = Math.Clamp(sampleYPx, 0, bitmap.Height - 1);

        var pixel = bitmap.GetPixel(sampleXPx, sampleYPx);

        // Amber blends to R≈255, G≈233, B≈168 over white — blue is distinctly lower than green.
        pixel.Green.ShouldBeGreaterThan(
            pixel.Blue,
            $"Bank-tier amber pixel at ({sampleXPx},{sampleYPx}) must have G>B: R={pixel.Red} G={pixel.Green} B={pixel.Blue}.");
    }
#pragma warning restore CA1416

    /// <summary>
    /// A Condusef-tier FAIL finding must still render red (regulatory default), even though
    /// a tier map is explicitly provided.  Asserts R > G just as the baseline Test 3 does.
    /// </summary>
    [Fact]
#pragma warning disable CA1416
    public void Generate_CondusefTierCheckId_HighlightPixelIsRedTinted()
    {
        const double pdfPigLeft = 100, pdfPigBottom = 400, boxWidth = 200, boxHeight = 30, pageH = 842;
        var inputPdf = BuildTestPdf(1);
        var finding  = FailFindingWithBox("CL-COND", left: pdfPigLeft, bottom: pdfPigBottom, width: boxWidth, height: boxHeight);
        var tiers    = new Dictionary<string, ChecklistTier> { ["CL-COND"] = ChecklistTier.Condusef };

        var result = _generator.Generate(inputPdf, new[] { finding }, TestContext.Current.CancellationToken, tiers);
        result.IsSuccess.ShouldBeTrue();

        const int dpi = 150;
        const double ptPerInch = 72.0;
        double scale = dpi / ptPerInch;

        using var stream = new MemoryStream(result.Value!);
        using var bitmap = Conversion.ToImage(stream, leaveOpen: false, page: 0, options: new RenderOptions(Dpi: dpi));

        var sampleXPx = (int)((pdfPigLeft + boxWidth  / 2.0) * scale);
        var sampleYPx = (int)(((pageH - pdfPigBottom - boxHeight) + boxHeight / 2.0) * scale);
        sampleXPx = Math.Clamp(sampleXPx, 0, bitmap.Width  - 1);
        sampleYPx = Math.Clamp(sampleYPx, 0, bitmap.Height - 1);

        var pixel = bitmap.GetPixel(sampleXPx, sampleYPx);

        pixel.Red.ShouldBeGreaterThan(
            pixel.Green,
            $"Condusef-tier must be red (R>G) at ({sampleXPx},{sampleYPx}): R={pixel.Red} G={pixel.Green} B={pixel.Blue}.");
    }
#pragma warning restore CA1416

    /// <summary>
    /// When a non-null tier map is supplied but the CheckId is not present in the map,
    /// the highlight must fall back to red (conservative / abstain-safe default).
    /// </summary>
    [Fact]
#pragma warning disable CA1416
    public void Generate_UnmappedCheckIdWithNonNullMap_HighlightPixelIsRedTinted()
    {
        const double pdfPigLeft = 100, pdfPigBottom = 400, boxWidth = 200, boxHeight = 30, pageH = 842;
        var inputPdf = BuildTestPdf(1);
        var finding  = FailFindingWithBox("CL-UNMAP", left: pdfPigLeft, bottom: pdfPigBottom, width: boxWidth, height: boxHeight);
        // Map exists but does not contain "CL-UNMAP" — must default to red.
        var tiers = new Dictionary<string, ChecklistTier> { ["SOME-OTHER"] = ChecklistTier.Bank };

        var result = _generator.Generate(inputPdf, new[] { finding }, TestContext.Current.CancellationToken, tiers);
        result.IsSuccess.ShouldBeTrue();

        const int dpi = 150;
        const double ptPerInch = 72.0;
        double scale = dpi / ptPerInch;

        using var stream = new MemoryStream(result.Value!);
        using var bitmap = Conversion.ToImage(stream, leaveOpen: false, page: 0, options: new RenderOptions(Dpi: dpi));

        var sampleXPx = (int)((pdfPigLeft + boxWidth  / 2.0) * scale);
        var sampleYPx = (int)(((pageH - pdfPigBottom - boxHeight) + boxHeight / 2.0) * scale);
        sampleXPx = Math.Clamp(sampleXPx, 0, bitmap.Width  - 1);
        sampleYPx = Math.Clamp(sampleYPx, 0, bitmap.Height - 1);

        var pixel = bitmap.GetPixel(sampleXPx, sampleYPx);

        pixel.Red.ShouldBeGreaterThan(
            pixel.Green,
            $"Unmapped CheckId must default to red (R>G) at ({sampleXPx},{sampleYPx}): R={pixel.Red} G={pixel.Green} B={pixel.Blue}.");
    }
#pragma warning restore CA1416

    /// <summary>
    /// Null <c>checklistTiers</c> must produce exactly the same red highlight as
    /// calling the original 3-parameter Generate — backward compatibility regression.
    /// </summary>
    [Fact]
#pragma warning disable CA1416
    public void Generate_NullChecklistTiers_HighlightPixelIsRedTinted()
    {
        const double pdfPigLeft = 100, pdfPigBottom = 400, boxWidth = 200, boxHeight = 30, pageH = 842;
        var inputPdf = BuildTestPdf(1);
        var finding  = FailFindingWithBox("CL-NULL-MAP", left: pdfPigLeft, bottom: pdfPigBottom, width: boxWidth, height: boxHeight);

        // Explicitly pass null for checklistTiers — must reproduce original behaviour.
        var result = _generator.Generate(inputPdf, new[] { finding }, TestContext.Current.CancellationToken, checklistTiers: null);
        result.IsSuccess.ShouldBeTrue();

        const int dpi = 150;
        const double ptPerInch = 72.0;
        double scale = dpi / ptPerInch;

        using var stream = new MemoryStream(result.Value!);
        using var bitmap = Conversion.ToImage(stream, leaveOpen: false, page: 0, options: new RenderOptions(Dpi: dpi));

        var sampleXPx = (int)((pdfPigLeft + boxWidth  / 2.0) * scale);
        var sampleYPx = (int)(((pageH - pdfPigBottom - boxHeight) + boxHeight / 2.0) * scale);
        sampleXPx = Math.Clamp(sampleXPx, 0, bitmap.Width  - 1);
        sampleYPx = Math.Clamp(sampleYPx, 0, bitmap.Height - 1);

        var pixel = bitmap.GetPixel(sampleXPx, sampleYPx);

        pixel.Red.ShouldBeGreaterThan(
            pixel.Green,
            $"Null tier map must produce red (R>G) at ({sampleXPx},{sampleYPx}): R={pixel.Red} G={pixel.Green} B={pixel.Blue}.");
    }
#pragma warning restore CA1416

    /// <summary>
    /// A Both-tier FAIL finding must render red, not amber, because it belongs to the
    /// regulatory CONDUSEF floor (conservative default).
    /// </summary>
    [Fact]
#pragma warning disable CA1416
    public void Generate_BothTierCheckId_HighlightPixelIsRedTinted()
    {
        const double pdfPigLeft = 100, pdfPigBottom = 400, boxWidth = 200, boxHeight = 30, pageH = 842;
        var inputPdf = BuildTestPdf(1);
        var finding  = FailFindingWithBox("CL-BOTH", left: pdfPigLeft, bottom: pdfPigBottom, width: boxWidth, height: boxHeight);
        var tiers    = new Dictionary<string, ChecklistTier> { ["CL-BOTH"] = ChecklistTier.Both };

        var result = _generator.Generate(inputPdf, new[] { finding }, TestContext.Current.CancellationToken, tiers);
        result.IsSuccess.ShouldBeTrue();

        const int dpi = 150;
        const double ptPerInch = 72.0;
        double scale = dpi / ptPerInch;

        using var stream = new MemoryStream(result.Value!);
        using var bitmap = Conversion.ToImage(stream, leaveOpen: false, page: 0, options: new RenderOptions(Dpi: dpi));

        var sampleXPx = (int)((pdfPigLeft + boxWidth  / 2.0) * scale);
        var sampleYPx = (int)(((pageH - pdfPigBottom - boxHeight) + boxHeight / 2.0) * scale);
        sampleXPx = Math.Clamp(sampleXPx, 0, bitmap.Width  - 1);
        sampleYPx = Math.Clamp(sampleYPx, 0, bitmap.Height - 1);

        var pixel = bitmap.GetPixel(sampleXPx, sampleYPx);

        pixel.Red.ShouldBeGreaterThan(
            pixel.Green,
            $"Both-tier must be red (R>G) at ({sampleXPx},{sampleYPx}): R={pixel.Red} G={pixel.Green} B={pixel.Blue}.");
    }
#pragma warning restore CA1416

    /// <summary>
    /// Multiple FAIL findings (mix of bbox + page-hint) produce a valid PDF with
    /// sequential callout numbers — structural verification that the callout counter
    /// advances and no crash occurs across 3 annotated findings.
    /// </summary>
    [Fact]
    public void Generate_MultipleFailFindings_ValidPdfWithCallouts()
    {
        var inputPdf = BuildTestPdf(2);
        var tiers = new Dictionary<string, ChecklistTier>
        {
            ["CL-C1"] = ChecklistTier.Condusef,
            ["CL-B2"] = ChecklistTier.Bank,
        };
        var findings = new[]
        {
            FailFindingWithBox("CL-C1",  pageNumber: 1, left: 50,  bottom: 600, width: 100, height: 20),
            FailFindingWithBox("CL-B2",  pageNumber: 2, left: 100, bottom: 400, width: 200, height: 30),
            FailFindingPageHint("CL-P3", page: 1),
        };

        var result = _generator.Generate(inputPdf, findings, TestContext.Current.CancellationToken, tiers);

        result.IsSuccess.ShouldBeTrue("Mixed-tier findings with 3 callouts must succeed.");
        result.Value.ShouldNotBeNull();
        result.Value!.SequenceEqual(inputPdf).ShouldBeFalse("Callouts must modify the PDF.");

        using var outputStream = new MemoryStream(result.Value);
        using var outputDoc = PdfReader.Open(outputStream, PdfDocumentOpenMode.Import);
        outputDoc.PageCount.ShouldBe(2, "Page count must be preserved with callouts.");
    }
}
