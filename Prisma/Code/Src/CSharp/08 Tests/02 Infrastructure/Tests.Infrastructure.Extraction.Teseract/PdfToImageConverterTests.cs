using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// Regression tests for <see cref="PdfToImageConverter"/> — specifically the page-index
/// off-by-one bug (G-M2 / FR6) where the old exception-as-sentinel loop emitted
/// "Stopping PDF conversion at page N: ArgumentOutOfRangeException" for every PDF.
/// </summary>
public class PdfToImageConverterTests
{
    private readonly ILogger<PdfToImageConverter> _logger;
    private readonly PdfToImageConverter _converter;

    public PdfToImageConverterTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<PdfToImageConverter>(output);
        _converter = new PdfToImageConverter(_logger);
    }

    /// <summary>
    /// Builds a minimal but valid N-page PDF entirely in memory (no extra library required).
    /// Each page is a blank A4 sheet which PDFtoImage/SkiaSharp can rasterise.
    /// </summary>
    private static byte[] BuildMinimalPdf(int pageCount)
    {
        // A minimal valid PDF structure: header, catalog, pages, N page objects, xref + trailer.
        // Each page is blank (no content stream) — sufficient for rasterisation.

        var sb = new System.Text.StringBuilder();

        // Object offsets for the xref table
        // Object numbering: 1=catalog, 2=pages, 3..(2+pageCount)=page objects
        int totalObjects = 2 + pageCount;
        var offsets = new int[totalObjects + 1]; // 1-indexed

        // --- Header ---
        sb.Append("%PDF-1.4\n");

        // --- Object 1: Catalog ---
        offsets[1] = sb.Length;
        sb.Append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // --- Object 2: Pages (parent node) ---
        offsets[2] = sb.Length;
        sb.Append("2 0 obj\n<< /Type /Pages /Kids [");
        for (int i = 0; i < pageCount; i++)
        {
            sb.Append($"{3 + i} 0 R ");
        }
        sb.Append($"] /Count {pageCount} >>\nendobj\n");

        // --- Object 3..(2+pageCount): Individual page objects ---
        for (int i = 0; i < pageCount; i++)
        {
            int objNum = 3 + i;
            offsets[objNum] = sb.Length;
            // Blank A4 page (595 x 842 pt), no content stream
            sb.Append($"{objNum} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>\nendobj\n");
        }

        // --- Cross-reference table ---
        int xrefOffset = sb.Length;
        sb.Append($"xref\n0 {totalObjects + 1}\n");
        sb.Append("0000000000 65535 f \n");
        for (int i = 1; i <= totalObjects; i++)
        {
            sb.Append($"{offsets[i]:D10} 00000 n \n");
        }

        // --- Trailer ---
        sb.Append($"trailer\n<< /Size {totalObjects + 1} /Root 1 0 R >>\n");
        sb.Append($"startxref\n{xrefOffset}\n%%EOF\n");

        return System.Text.Encoding.Latin1.GetBytes(sb.ToString());
    }

    [Fact]
    [Trait("category", "fast")]
    public async Task ConvertToImagesAsync_ThreePagePdf_ReturnsThreeImages_WithNoException()
    {
        // Arrange — G-M2 regression: a 3-page PDF previously triggered
        // "Stopping PDF conversion at page 3: ArgumentOutOfRangeException"
        // because the loop iterated one past the last valid page index.
        var pdfBytes = BuildMinimalPdf(pageCount: 3);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await _converter.ConvertToImagesAsync(pdfBytes, dpi: 72, cancellationToken: ct)
            ;

        // Assert
        result.IsSuccess.ShouldBeTrue($"Expected conversion to succeed; failure: {result.Error}");
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(3, "All 3 pages must be converted — not 2 (off-by-one) and not a thrown exception");
        foreach (var pageBytes in result.Value)
        {
            pageBytes.ShouldNotBeEmpty("Each page image must contain PNG bytes");
        }
    }

    [Fact]
    [Trait("category", "fast")]
    public async Task ConvertToImagesAsync_SinglePagePdf_ReturnsOneImage()
    {
        // Arrange
        var pdfBytes = BuildMinimalPdf(pageCount: 1);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await _converter.ConvertToImagesAsync(pdfBytes, dpi: 72, cancellationToken: ct)
            ;

        // Assert
        result.IsSuccess.ShouldBeTrue($"Expected conversion to succeed; failure: {result.Error}");
        result.Value!.Count.ShouldBe(1);
        result.Value[0].ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("category", "fast")]
    public async Task ConvertToImagesAsync_FivePagePdf_ReturnsAllFiveImages()
    {
        // Arrange — confirms the fix scales: any pageCount pages → exactly pageCount images
        var pdfBytes = BuildMinimalPdf(pageCount: 5);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await _converter.ConvertToImagesAsync(pdfBytes, dpi: 72, cancellationToken: ct)
            ;

        // Assert
        result.IsSuccess.ShouldBeTrue($"Expected conversion to succeed; failure: {result.Error}");
        result.Value!.Count.ShouldBe(5);
    }

    [Fact]
    [Trait("category", "fast")]
    public async Task ConvertToImagesAsync_NullBytes_ReturnsFailureResult()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await _converter.ConvertToImagesAsync(null!, dpi: 300, cancellationToken: ct)
            ;

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("category", "fast")]
    public async Task ConvertToImagesAsync_EmptyBytes_ReturnsFailureResult()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await _converter.ConvertToImagesAsync([], dpi: 300, cancellationToken: ct)
            ;

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrWhiteSpace();
    }
}
