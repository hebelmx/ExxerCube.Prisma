using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using ExxerCube.Prisma.Infrastructure.Imaging;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// Regression guard for the Linux native OCR SIGSEGV (exit 139) caused by Leptonica/Emgu symbol
/// interposition. Reproduces the §2 max-fidelity gate's Stage order IN ONE PROCESS:
/// SkiaSharp/PDFium render (FileSystemLoader) -> Emgu.CV Stage 1 quality analysis -> Tesseract Stage 2 OCR.
/// </summary>
/// <remarks>
/// Emgu's <c>libcvextern.so</c> statically bundles its own Leptonica and exports <c>pixCreate</c>/
/// <c>boxaCreate</c>/etc. with global ELF visibility. If OpenCV loads before the system Leptonica is in the
/// global symbol scope, the Tesseract NuGet's bundled <c>libleptonica-1.82.0.so</c> binds its own symbols
/// into OpenCV's incompatible copy (proven via <c>LD_DEBUG=bindings</c>) — a Pix-ABI split that segfaults.
/// <see cref="LeptonicaInteropGuard"/> (armed by the OCR assembly's module initializer at DI-composition
/// time) preloads the system Leptonica first and prevents it. This test fails with exit 139 if that guard
/// ever stops arming before Emgu's first native call. Confirmed: removing the guard makes this crash 139.
/// </remarks>
public class NativeCoexistenceRegressionTests
{
    private const string Pdf =
        "/home/abel/ExxerProjects/IndFusion/ExxerCube.Prisma/Prisma/Fixtures/PRP1/222AAA-44444444442025.pdf";

    private readonly ITestOutputHelper _output;

    public NativeCoexistenceRegressionTests(ITestOutputHelper output) => _output = output;

    [Fact(DisplayName = "OCR survives co-resident SkiaSharp + Emgu.CV (no native SIGSEGV)", Timeout = 300_000)]
    public async Task SkiaRender_ThenEmguQuality_ThenTesseractOcr_DoesNotSegfault()
    {
        // Skip cleanly if the fixture is unavailable on this box (keeps the suite green elsewhere).
        if (!File.Exists(Pdf))
        {
            Assert.Skip($"Fixture not present: {Pdf}");
            return;
        }

        var pdfBytes = await File.ReadAllBytesAsync(Pdf, TestContext.Current.CancellationToken);

        // STAGE order mirrors FileSystemLoader -> PolynomialImageQualityAnalyzer -> TesseractOcrExecutor.
        var converter = new PdfToImageConverter(XUnitLogger.CreateLogger<PdfToImageConverter>(_output));
        var rendered = await converter.ConvertToImagesAsync(pdfBytes, 300, TestContext.Current.CancellationToken);
        rendered.IsSuccess.ShouldBeTrue($"PDF rasterization should succeed: {rendered.Error}");
        var pagePng = rendered.Value![0];

        var analyzer = new PolynomialImageQualityAnalyzer(
            XUnitLogger.CreateLogger<PolynomialImageQualityAnalyzer>(_output));
        var quality = await analyzer.AnalyzeAsync(new ImageData(pagePng, Pdf)); // loads libcvextern.so
        quality.IsSuccess.ShouldBeTrue($"Stage 1 quality analysis should succeed: {quality.Error}");

        using var executor = new TesseractOcrExecutor(XUnitLogger.CreateLogger<TesseractOcrExecutor>(_output));
        var ocr = await executor.ExecuteOcrAsync(
            new ImageData(pagePng, Pdf),
            new OCRConfig(language: "spa", oem: 1, psm: 6, fallbackLanguage: "eng", confidenceThreshold: 0.6f));

        // If the interposition guard ever regresses, the process dies (139) before this assert runs.
        ocr.IsSuccess.ShouldBeTrue($"Stage 2 OCR should succeed without segfault: {ocr.Error}");
        ocr.Value!.Text.ShouldNotBeNullOrWhiteSpace("OCR should extract text from the rendered page");
    }
}
