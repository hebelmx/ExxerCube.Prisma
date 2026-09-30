using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Mutation-killing tests for <see cref="PdfMetadataExtractor"/>. The PDF direct-text path is a placeholder
/// that always yields empty, so every call routes through the (mocked) OCR pipeline and the deterministic
/// regex parsing — which is the whole worthy surface. OCR text is supplied directly, so real <c>\n</c> line
/// breaks make the <c>[^\n]+</c> captures (Causa/Acción) exact. The <c>usedDirectExtraction == true</c>
/// branch in <c>BuildExtractionMetadata</c> is unreachable given the placeholder (pinned as a floor).
/// </summary>
public class PdfMetadataExtractorMutationTests
{
    private readonly IImagePreprocessor _imagePreprocessor = Substitute.For<IImagePreprocessor>();
    private readonly IOcrExecutor _ocrExecutor = Substitute.For<IOcrExecutor>();
    private readonly PdfMetadataExtractor _extractor;

    public PdfMetadataExtractorMutationTests()
    {
        _extractor = new PdfMetadataExtractor(
            _ocrExecutor,
            _imagePreprocessor,
            Substitute.For<ILogger<PdfMetadataExtractor>>());
    }

    private void OcrYields(string text)
    {
        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(new ImageData { Data = new byte[] { 1 }, SourcePath = "p" }));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(new OCRResult { Text = text }));
    }

    private Task<Result<ExtractedMetadata>> Extract() =>
        _extractor.ExtractFromPdfAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 }, TestContext.Current.CancellationToken);

    // ---------------------------------------------------------------- happy path + quality metadata

    [Fact]
    public async Task ExtractFromPdfAsync_ExpedienteAreaRfc_PinsFieldsAndQualityMetadata()
    {
        OcrYields("A/AS1-2505-088637-PHM ASEGURAMIENTO PERJ800101ABC");

        var result = await Extract();

        result.IsSuccess.ShouldBeTrue();
        var m = result.Value!;
        m.Expediente.ShouldNotBeNull();
        m.Expediente!.NumeroExpediente.ShouldBe("A/AS1-2505-088637-PHM");
        m.Expediente.AreaDescripcion.ShouldBe("ASEGURAMIENTO");
        m.RfcValues.ShouldBe(new[] { "PERJ800101ABC" });
        m.ExtractedFields.ShouldNotBeNull();
        m.ExtractedFields!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");

        var q = m.QualityMetadata.ShouldNotBeNull();
        q.Source.ShouldBe(SourceType.PDF_OCR_CNBV);
        q.TotalFieldsExtracted.ShouldBe(3);
        q.RegexMatches.ShouldBe(2);
        q.CatalogValidations.ShouldBe(1);
        q.PatternViolations.ShouldBe(0);
        q.TotalWords.ShouldBe(3);
        q.LowConfidenceWords.ShouldBe(0);  // (int)(3 * 0.10) == 0  (kills '*' -> '/')
        q.MeanConfidence.ShouldBe(0.85);
        q.MinConfidence.ShouldBe(0.70);
        q.QualityIndex.ShouldBe(0.80);
    }

    [Fact]
    public async Task ExtractFromPdfAsync_NoExpediente_ExpedienteNullButFieldsStillBuilt()
    {
        // No expediente pattern -> Expediente null, QualityMetadata null, but ExtractedFields is still returned.
        OcrYields("CAUSA: Revision general\nsin numero de caso");

        var result = await Extract();

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBeNull();
        result.Value.QualityMetadata.ShouldBeNull();
        result.Value.ExtractedFields.ShouldNotBeNull();
        result.Value.ExtractedFields!.Causa.ShouldBe("Revision general");
    }

    // ---------------------------------------------------------------- ExtractCausa (CAUSA / MOTIVO)

    [Fact]
    public async Task ExtractFromPdfAsync_CausaLine_CapturedExactly()
    {
        OcrYields("A/AS1-2505-088637-PHM\nCAUSA: Bloqueo de cuentas\nfin");

        var result = await Extract();

        result.Value!.ExtractedFields!.Causa.ShouldBe("Bloqueo de cuentas");
    }

    [Fact]
    public async Task ExtractFromPdfAsync_MotivoLine_AlsoCapturedAsCausa()
    {
        // Second Causa pattern alternative (MOTIVO) — kills its removal.
        OcrYields("A/AS1-2505-088637-PHM\nMOTIVO: Lavado de dinero\nfin");

        var result = await Extract();

        result.Value!.ExtractedFields!.Causa.ShouldBe("Lavado de dinero");
    }

    // ---------------------------------------------------------------- ExtractAccionSolicitada

    [Fact]
    public async Task ExtractFromPdfAsync_SolicitaLine_CapturedAsAccion()
    {
        OcrYields("A/AS1-2505-088637-PHM\nSOLICITA embargar los bienes inmuebles\nfin");

        var result = await Extract();

        result.Value!.ExtractedFields!.AccionSolicitada.ShouldBe("embargar los bienes inmuebles");
    }

    [Fact]
    public async Task ExtractFromPdfAsync_NoActionPattern_FallsBackToFirst200Chars()
    {
        // No ACCIÓN/SOLICITA/REQUERIMIENTO pattern and text < 200 chars -> fallback returns the whole text.
        OcrYields("A/AS1-2505-088637-PHM texto breve");

        var result = await Extract();

        result.Value!.ExtractedFields!.AccionSolicitada.ShouldBe("A/AS1-2505-088637-PHM texto breve");
    }

    // ---------------------------------------------------------------- ExtractMontos

    [Fact]
    public async Task ExtractFromPdfAsync_MontoLine_ParsedDistinctMxnAmount()
    {
        // No expediente digits in this text so the only amount captured is 5,000.00.
        OcrYields("MONTO: $5,000.00 pesos");

        var result = await Extract();

        var montos = result.Value!.ExtractedFields!.Montos;
        montos.ShouldNotBeNull();
        montos.Count.ShouldBe(1);
        montos[0].Value.ShouldBe(5000.00m);
        montos[0].Currency.ShouldBe("MXN");
    }

    // ---------------------------------------------------------------- ExtractDates -> Fechas

    [Fact]
    public async Task ExtractFromPdfAsync_IsoDate_FormattedIntoFechas()
    {
        OcrYields("A/AS1-2505-088637-PHM fecha 2025-01-15");

        var result = await Extract();

        result.Value!.ExtractedFields!.Fechas.ShouldContain("2025-01-15");
        result.Value.Dates.ShouldBe(new[] { new DateTime(2025, 1, 15) });
    }

    // ---------------------------------------------------------------- failure / pipeline branches

    [Fact]
    public async Task ExtractFromPdfAsync_PreprocessFails_ReturnsFailure()
    {
        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.WithFailure("preprocess-boom"));

        var result = await Extract();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("preprocess-boom");
    }

    [Fact]
    public async Task ExtractFromPdfAsync_OcrFails_ReturnsFailure()
    {
        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(new ImageData { Data = new byte[] { 1 }, SourcePath = "p" }));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.WithFailure("ocr-boom"));

        var result = await Extract();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Failed to extract text from PDF"); // pins the L58 prefix literal
        result.Error.ShouldContain("ocr-boom");
    }

    [Fact]
    public async Task ExtractFromPdfAsync_OcrYieldsEmpty_ReturnsNoTextFailure()
    {
        OcrYields(string.Empty); // OCR succeeds but with empty text -> "No text extracted from PDF"

        var result = await Extract();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("No text extracted from PDF");
    }

    [Fact]
    public async Task ExtractFromPdfAsync_PreprocessReturnsNullValue_ReturnsFailure()
    {
        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(null!));

        var result = await Extract();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Preprocessed image is null");
    }

    [Fact]
    public async Task ExtractFromPdfAsync_OcrReturnsNullValue_ReturnsFailure()
    {
        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(new ImageData { Data = new byte[] { 1 }, SourcePath = "p" }));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(null!));

        var result = await Extract();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("OCR result is null");
    }

    [Fact]
    public async Task ExtractFromPdfAsync_AuthorityInText_PopulatesSourceAuthorityCode()
    {
        OcrYields("A/AS1-2505-088637-PHM SUBDELEGACION 5 ZONA NORTE");

        var result = await Extract();

        var law = result.Value!.Expediente!.LawMandatedFields.ShouldNotBeNull();
        law.SourceAuthorityCode.ShouldBe("SUBDELEGACION 5 ZONA NORTE");
    }

    // ---------------------------------------------------------------- ExtractTextAsync

    [Fact]
    public async Task ExtractTextAsync_OcrText_ReturnsOcrValue()
    {
        OcrYields("texto extraido por ocr suficientemente largo para todo");

        var result = await _extractor.ExtractTextAsync(
            new byte[] { 0x25, 0x50, 0x44, 0x46 }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("texto extraido por ocr suficientemente largo para todo");
    }

    [Fact]
    public async Task ExtractTextAsync_PreCancelled_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _extractor.ExtractTextAsync(new byte[] { 0x25, 0x50 }, cts.Token);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractTextAsync_OcrFails_ReturnsFailure()
    {
        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(new ImageData { Data = new byte[] { 1 }, SourcePath = "p" }));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.WithFailure("ocr-text-boom"));

        var result = await _extractor.ExtractTextAsync(
            new byte[] { 0x25, 0x50 }, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Failed to extract text from PDF"); // pins the L174 prefix literal
        result.Error.ShouldContain("ocr-text-boom");
    }

    [Fact]
    public async Task ExtractTextAsync_OcrYieldsEmpty_ReturnsNoTextFailure()
    {
        OcrYields(string.Empty);

        var result = await _extractor.ExtractTextAsync(
            new byte[] { 0x25, 0x50 }, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("No text extracted from PDF");
    }

    // ---------------------------------------------------------------- unsupported formats

    [Fact]
    public async Task ExtractFromXmlAsync_ReturnsFailureMentioningXmlExtractor()
    {
        var result = await _extractor.ExtractFromXmlAsync(new byte[] { 1 }, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("XmlMetadataExtractor");
    }

    [Fact]
    public async Task ExtractFromDocxAsync_ReturnsFailureMentioningDocxExtractor()
    {
        var result = await _extractor.ExtractFromDocxAsync(new byte[] { 1 }, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("DocxMetadataExtractor");
    }
}
