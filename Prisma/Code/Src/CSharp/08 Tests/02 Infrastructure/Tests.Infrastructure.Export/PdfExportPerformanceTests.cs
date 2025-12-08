namespace ExxerCube.Prisma.Tests.Infrastructure.Export;

/// <summary>
/// Performance tests for PDF export operations to verify NFR10 and NFR11 requirements.
/// </summary>
public class PdfExportPerformanceTests
{
    private readonly IPdfRequirementSummarizer _pdfSummarizer;
    private readonly IResponseExporter _pdfSigner;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfExportPerformanceTests"/> class.
    /// </summary>
    public PdfExportPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _metadataExtractor = Substitute.For<IMetadataExtractor>();
        _pdfSummarizer = new PdfRequirementSummarizerService(
            _metadataExtractor,
            XUnitLogger.CreateLogger<PdfRequirementSummarizerService>(output));

        var certificateOptions = Options.Create(new CertificateOptions
        {
            Source = "File",
            FilePath = "test-cert.pfx",
            Password = "test"
        });
        _pdfSigner = new DigitalPdfSigner(
            certificateOptions,
            XUnitLogger.CreateLogger<DigitalPdfSigner>(output));
    }

    /// <summary>
    /// Creates a sample PDF content for testing.
    /// </summary>
    private static byte[] CreateSamplePdfContent()
    {
        // Create a minimal valid PDF structure
        // PDF header + minimal structure for testing
        var pdfContent = @"%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
>>
endobj
2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj
3 0 obj
<<
/Type /Page
/Parent 2 0 R
/MediaBox [0 0 612 792]
/Contents 4 0 R
>>
endobj
4 0 obj
<<
/Length 44
>>
stream
BT
/F1 12 Tf
100 700 Td
(Test PDF Content) Tj
ET
endstream
endobj
xref
0 5
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000206 00000 n
trailer
<<
/Size 5
/Root 1 0 R
>>
startxref
300
%%EOF";
        return System.Text.Encoding.UTF8.GetBytes(pdfContent);
    }

    /// <summary>
    /// Creates sample PDF text content for testing summarization.
    /// </summary>
    private static string CreateSamplePdfText()
    {
        return @"REQUERIMIENTO DE BLOQUEO
Se requiere bloquear la cuenta bancaria del cliente debido a orden judicial.
Artículo 123 de la Ley de Prevención de Lavado de Dinero.

REQUERIMIENTO DE DESBLOQUEO
Una vez cumplidos los requisitos, se procederá al desbloqueo de la cuenta.
Artículo 456 del Reglamento.

REQUERIMIENTO DE DOCUMENTACIÓN
El cliente debe presentar documentación adicional para verificación.
Documentos requeridos: identificación oficial, comprobante de domicilio.

REQUERIMIENTO DE TRANSFERENCIA
Se requiere transferir fondos a cuenta designada por autoridad competente.
Monto: $100,000.00 MXN

REQUERIMIENTO DE INFORMACIÓN
Se solicita información sobre transacciones realizadas en los últimos 6 meses.
Período: Enero 2024 - Junio 2024";
    }

    /// <summary>
    /// Creates ExtractedMetadata from text content for mocking.
    /// </summary>
    private static ExtractedMetadata CreateExtractedMetadataFromText(string text)
    {
        return new ExtractedMetadata
        {
            ExtractedFields = new ExtractedFields
            {
                // Put the full text in AccionSolicitada so ReconstructTextFromExtractedFields can extract it
                AccionSolicitada = text,
                Expediente = "A/AS1-2505-088637-PHM",
                Causa = "Test causa"
            }
        };
    }

    /// <summary>
    /// Tests that SummarizeRequirementsAsync completes within 10 seconds (NFR10).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task SummarizeRequirementsAsync_CompletesWithin10Seconds_NFR10()
    {
        // Arrange
        var pdfContent = CreateSamplePdfContent();
        var pdfText = CreateSamplePdfText();
        var extractedMetadata = CreateExtractedMetadataFromText(pdfText);

        // Mock ExtractFromPdfAsync - the service will try PdfSharp first (which will fail), then fall back to this
        _metadataExtractor.ExtractFromPdfAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtractedMetadata>.Success(extractedMetadata));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _pdfSummarizer.SummarizeRequirementsAsync(pdfContent, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(10000,
            $"SummarizeRequirementsAsync took {stopwatch.ElapsedMilliseconds}ms, exceeding NFR10 target of <10s (10000ms)");

        _output.WriteLine($"SummarizeRequirementsAsync completed in {stopwatch.ElapsedMilliseconds}ms (NFR10 target: <10000ms)");
    }

    /// <summary>
    /// Tests that SummarizeRequirementsFromTextAsync completes within 10 seconds (NFR10).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task SummarizeRequirementsFromTextAsync_CompletesWithin10Seconds_NFR10()
    {
        // Arrange
        var pdfText = CreateSamplePdfText();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _pdfSummarizer.SummarizeRequirementsFromTextAsync(pdfText, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(10000,
            $"SummarizeRequirementsFromTextAsync took {stopwatch.ElapsedMilliseconds}ms, exceeding NFR10 target of <10s (10000ms)");

        _output.WriteLine($"SummarizeRequirementsFromTextAsync completed in {stopwatch.ElapsedMilliseconds}ms (NFR10 target: <10000ms)");
    }

    /// <summary>
    /// Tests that SummarizeRequirementsAsync handles large PDFs efficiently.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task SummarizeRequirementsAsync_LargePdf_HandlesEfficiently()
    {
        // Arrange - Create large text content
        var largeText = CreateSamplePdfText();
        // Repeat the text multiple times to simulate a large PDF
        largeText = string.Join("\n", new string[100].Select(_ => largeText));

        var extractedMetadata = CreateExtractedMetadataFromText(largeText);
        var pdfContent = CreateSamplePdfContent();

        // Mock ExtractFromPdfAsync - the service will try PdfSharp first (which will fail), then fall back to this
        _metadataExtractor.ExtractFromPdfAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtractedMetadata>.Success(extractedMetadata));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _pdfSummarizer.SummarizeRequirementsAsync(pdfContent, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        // Large PDFs should still complete within reasonable time (30 seconds)
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(30000,
            $"SummarizeRequirementsAsync for large PDF took {stopwatch.ElapsedMilliseconds}ms, exceeding target of <30s (30000ms)");

        _output.WriteLine($"SummarizeRequirementsAsync for large PDF completed in {stopwatch.ElapsedMilliseconds}ms");
    }
}