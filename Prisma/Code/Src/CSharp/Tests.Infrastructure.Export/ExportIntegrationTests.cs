namespace ExxerCube.Prisma.Tests.Infrastructure.Export;

/// <summary>
/// Integration tests for PDF summarization and digital signing workflow.
/// </summary>
public class ExportIntegrationTests
{

    /// <summary>
    /// Tests end-to-end PDF summarization workflow with sample PDF content.
    /// </summary>
    [Fact]
    public async Task PdfRequirementSummarizerService_EndToEnd_SummarizesRequirements()
    {
        // Arrange
        // Mock IMetadataExtractor instead of instantiating Infrastructure.Extraction types
        var metadataExtractor = Substitute.For<IMetadataExtractor>();
        var logger = XUnitLogger.CreateLogger<PdfRequirementSummarizerService>();
        var summarizer = new PdfRequirementSummarizerService(metadataExtractor, logger);

        // Create sample PDF text content (simulating extracted text)
        var pdfText = @"
            EXPEDIENTE: A/AS1-2505-088637-PHM
            REQUERIMIENTO 1: Se solicita el bloqueo de la cuenta 1234567890
            BLOQUEO: Congelar fondos por monto de $50,000.00

            REQUERIMIENTO 2: Se solicita presentar documentación fiscal
            DOCUMENTACIÓN: Proporcionar estados de cuenta de los últimos 6 meses

            REQUERIMIENTO 3: Se solicita información sobre movimientos
            INFORMACIÓN: Reportar transacciones del último mes
        ";

        // Act
        var result = await summarizer.SummarizeRequirementsFromTextAsync(pdfText, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Requirements.ShouldNotBeEmpty();
        result.Value.Bloqueo.ShouldNotBeEmpty();
        result.Value.Documentacion.ShouldNotBeEmpty();
        result.Value.Informacion.ShouldNotBeEmpty();
        result.Value.SummaryText.ShouldNotBeNullOrWhiteSpace();
        result.Value.ConfidenceScore.ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Tests that PDF summarization uses existing OCR text extraction without breaking OCR pipeline (IV1).
    /// </summary>
    [Fact]
    public async Task PdfRequirementSummarizerService_UsesOcrExtraction_DoesNotBreakPipeline()
    {
        // Arrange
        // Mock IMetadataExtractor instead of instantiating Infrastructure.Extraction types
        var metadataExtractor = Substitute.For<IMetadataExtractor>();
        var logger = XUnitLogger.CreateLogger<PdfRequirementSummarizerService>();
        var summarizer = new PdfRequirementSummarizerService(metadataExtractor, logger);

        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // PDF header
        
        // Configure mock to return ExtractedMetadata with text in ExtractedFields
        // PdfRequirementSummarizerService reconstructs text from ExtractedFields
        var extractedMetadata = new ExtractedMetadata
        {
            ExtractedFields = new ExtractedFields
            {
                Expediente = "TEST-001",
                Causa = "Test causa",
                AccionSolicitada = "BLOQUEO: Bloquear cuenta 1234567890"
            }
        };

        metadataExtractor.ExtractFromPdfAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtractedMetadata>.Success(extractedMetadata));

        // Act
        var result = await summarizer.SummarizeRequirementsAsync(pdfContent, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        // Verify metadata extractor was called (OCR pipeline integration verified through mock)
        await metadataExtractor.Received().ExtractFromPdfAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that certificate integration handles certificate unavailability gracefully (IV3).
    /// </summary>
    [Fact]
    public async Task DigitalPdfSigner_CertificateUnavailable_HandlesGracefully()
    {
        // Arrange
        var options = new CertificateOptions
        {
            Source = "File",
            FileCertificatePath = "nonexistent.pfx",
            FallbackToFile = false
        };
        var certificateOptions = Options.Create(options);
        var logger = XUnitLogger.CreateLogger<DigitalPdfSigner>();
        var signer = new DigitalPdfSigner(certificateOptions, logger);

        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "TEST-001",
                NumeroOficio = "OF-2024-001"
            }
        };
        using var stream = new MemoryStream();

        // Act
        var result = await signer.ExportSignedPdfAsync(metadata, stream, TestContext.Current.CancellationToken);

        // Assert
        // Should fail gracefully with error logging, not throw exception
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("certificate", Case.Insensitive);
    }
}