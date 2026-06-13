using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Tests native (embedded) PDF text extraction (MVP-PATH 2.2 / B2): a searchable PDF must yield its text via
/// PdfPig <em>without</em> falling back to OCR, while scanned/empty PDFs still use the OCR path.
/// </summary>
public sealed class PdfNativeTextExtractionTests
{
    private readonly IImagePreprocessor _imagePreprocessor = Substitute.For<IImagePreprocessor>();
    private readonly IOcrExecutor _ocrExecutor = Substitute.For<IOcrExecutor>();
    private readonly PdfMetadataExtractor _extractor;

    public PdfNativeTextExtractionTests()
    {
        _extractor = new PdfMetadataExtractor(
            _ocrExecutor, _imagePreprocessor, Substitute.For<ILogger<PdfMetadataExtractor>>());
    }

    /// <summary>Builds a single-page searchable PDF whose page contains the given text as selectable glyphs.</summary>
    private static byte[] BuildSearchablePdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842); // A4 in points
        if (!string.IsNullOrEmpty(text))
        {
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            page.AddText(text, 12, new PdfPoint(50, 700), font);
        }

        return builder.Build();
    }

    [Fact]
    public async Task ExtractTextAsync_SearchablePdf_ReturnsEmbeddedTextWithoutOcr()
    {
        // Arrange - a real searchable PDF with > 50 chars of embedded text (so the OCR-fallback branch is skipped).
        const string embedded = "Expediente: A/AS1-2505-088637-PHM Area: ASEGURAMIENTO RFC: PERJ800101ABC";
        var pdf = BuildSearchablePdf(embedded);

        // Act
        var result = await _extractor.ExtractTextAsync(pdf, TestContext.Current.CancellationToken);

        // Assert - the embedded text comes back, and OCR/preprocessing were never invoked.
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("A/AS1-2505-088637-PHM");
        result.Value.ShouldContain("ASEGURAMIENTO");

        await _ocrExecutor.DidNotReceive().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
        await _imagePreprocessor.DidNotReceive().PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>());
    }

    [Fact]
    public async Task ExtractTextAsync_NoEmbeddedText_FallsBackToOcr()
    {
        // Arrange - a PDF with no extractable text layer (empty page) must trigger the OCR fallback.
        var pdf = BuildSearchablePdf(string.Empty);
        var ocrText = "Expediente: B/BS2-2506-099999-XYZ Area: COBRANZA RFC: GOMA900202QabcExtra";
        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(new ImageData { Data = pdf, SourcePath = "scanned.pdf" }));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(new OCRResult { Text = ocrText }));

        // Act
        var result = await _extractor.ExtractTextAsync(pdf, TestContext.Current.CancellationToken);

        // Assert - OCR produced the text, proving the fallback path is intact.
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("COBRANZA");
        await _ocrExecutor.Received().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
    }
}
