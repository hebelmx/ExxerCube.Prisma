namespace ExxerCube.Prisma.Tests.Infrastructure.Export;

/// <summary>
/// Unit tests for <see cref="PdfRequirementSummarizerService"/>.
/// </summary>
public class PdfRequirementSummarizerServiceTests
{
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly ILogger<PdfRequirementSummarizerService> _logger;
    private readonly PdfRequirementSummarizerService _summarizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfRequirementSummarizerServiceTests"/> class.
    /// </summary>
    public PdfRequirementSummarizerServiceTests()
    {
        _metadataExtractor = Substitute.For<IMetadataExtractor>();
        _logger = XUnitLogger.CreateLogger<PdfRequirementSummarizerService>();
        _summarizer = new PdfRequirementSummarizerService(_metadataExtractor, _logger);
    }

    /// <summary>
    /// Tests that PDF requirement summarization succeeds with valid PDF content.
    /// </summary>
    [Fact]
    public async Task SummarizeRequirementsAsync_ValidPdfContent_ReturnsRequirementSummary()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // PDF header
        var extractedMetadata = new ExtractedMetadata
        {
            ExtractedFields = new ExtractedFields
            {
                Expediente = "A/AS1-2505-088637-PHM",
                Causa = "Test causa",
                AccionSolicitada = "Bloqueo de cuenta"
            }
        };

        _metadataExtractor.ExtractFromPdfAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtractedMetadata>.Success(extractedMetadata));

        // Act
        var result = await _summarizer.SummarizeRequirementsAsync(pdfContent, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Requirements.ShouldNotBeNull();
        result.Value.ExtractedAt.ShouldBeGreaterThan(DateTime.MinValue);
        await _metadataExtractor.Received().ExtractFromPdfAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that PDF requirement summarization fails when PDF content is null.
    /// </summary>
    [Fact]
    public async Task SummarizeRequirementsAsync_NullPdfContent_ReturnsFailure()
    {
        // Arrange
        byte[]? pdfContent = null;

        // Act
        var result = await _summarizer.SummarizeRequirementsAsync(pdfContent!, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("null");
    }

    /// <summary>
    /// Tests that PDF requirement summarization fails when PDF content is empty.
    /// </summary>
    [Fact]
    public async Task SummarizeRequirementsAsync_EmptyPdfContent_ReturnsFailure()
    {
        // Arrange
        var pdfContent = Array.Empty<byte>();

        // Act
        var result = await _summarizer.SummarizeRequirementsAsync(pdfContent, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("empty");
    }

    /// <summary>
    /// Tests that PDF requirement summarization categorizes bloqueo requirements correctly.
    /// </summary>
    [Fact]
    public async Task SummarizeRequirementsFromTextAsync_BloqueoRequirements_CategorizesCorrectly()
    {
        // Arrange
        var pdfText = @"
            REQUERIMIENTO: Se solicita el bloqueo de la cuenta 1234567890
            BLOQUEO: Congelar fondos por monto de $50,000.00
            Se requiere inmovilizar los recursos de la cuenta mencionada.
        ";

        // Act
        var result = await _summarizer.SummarizeRequirementsFromTextAsync(pdfText, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Bloqueo.ShouldNotBeEmpty();
        result.Value.Bloqueo.Count.ShouldBeGreaterThan(0);
        result.Value.Bloqueo.Any(r => r.Descripcion.Contains("bloqueo", StringComparison.OrdinalIgnoreCase) ||
                                      r.Descripcion.Contains("congelar", StringComparison.OrdinalIgnoreCase) ||
                                      r.Descripcion.Contains("inmovilizar", StringComparison.OrdinalIgnoreCase))
            .ShouldBeTrue();
    }

    /// <summary>
    /// Tests that PDF requirement summarization categorizes desbloqueo requirements correctly.
    /// </summary>
    [Fact]
    public async Task SummarizeRequirementsFromTextAsync_DesbloqueoRequirements_CategorizesCorrectly()
    {
        // Arrange
        var pdfText = @"
            REQUERIMIENTO: Se solicita el desbloqueo de la cuenta 1234567890
            DESBLOQUEO: Liberar fondos previamente congelados
            Se requiere descongelar los recursos de la cuenta mencionada.
        ";

        // Act
        var result = await _summarizer.SummarizeRequirementsFromTextAsync(pdfText, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Desbloqueo.ShouldNotBeEmpty();
        result.Value.Desbloqueo.Count.ShouldBeGreaterThan(0);
        result.Value.Desbloqueo.Any(r => r.Descripcion.Contains("desbloqueo", StringComparison.OrdinalIgnoreCase) ||
                                         r.Descripcion.Contains("liberar", StringComparison.OrdinalIgnoreCase) ||
                                         r.Descripcion.Contains("descongelar", StringComparison.OrdinalIgnoreCase))
            .ShouldBeTrue();
    }

    /// <summary>
    /// Tests that PDF requirement summarization categorizes documentacion requirements correctly.
    /// </summary>
    [Fact]
    public async Task SummarizeRequirementsFromTextAsync_DocumentacionRequirements_CategorizesCorrectly()
    {
        // Arrange
        var pdfText = @"
            REQUERIMIENTO: Se solicita presentar documentación fiscal
            DOCUMENTACIÓN: Proporcionar estados de cuenta de los últimos 6 meses
            Se requiere entregar comprobantes de ingresos y egresos.
        ";

        // Act
        var result = await _summarizer.SummarizeRequirementsFromTextAsync(pdfText, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Documentacion.ShouldNotBeEmpty();
        result.Value.Documentacion.Count.ShouldBeGreaterThan(0);
        result.Value.Documentacion.Any(r => r.Descripcion.Contains("documentación", StringComparison.OrdinalIgnoreCase) ||
                                            r.Descripcion.Contains("presentar", StringComparison.OrdinalIgnoreCase) ||
                                            r.Descripcion.Contains("proporcionar", StringComparison.OrdinalIgnoreCase) ||
                                            r.Descripcion.Contains("entregar", StringComparison.OrdinalIgnoreCase))
            .ShouldBeTrue();
    }

    /// <summary>
    /// Tests that PDF requirement summarization categorizes transferencia requirements correctly.
    /// </summary>
    [Fact]
    public async Task SummarizeRequirementsFromTextAsync_TransferenciaRequirements_CategorizesCorrectly()
    {
        // Arrange
        var pdfText = @"
            REQUERIMIENTO: Se solicita transferencia de fondos
            TRANSFERENCIA: Movilizar $100,000.00 a cuenta destino
            Se requiere realizar movimiento de recursos.
        ";

        // Act
        var result = await _summarizer.SummarizeRequirementsFromTextAsync(pdfText, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Transferencia.ShouldNotBeEmpty();
        result.Value.Transferencia.Count.ShouldBeGreaterThan(0);
        result.Value.Transferencia.Any(r => r.Descripcion.Contains("transferencia", StringComparison.OrdinalIgnoreCase) ||
                                           r.Descripcion.Contains("transferir", StringComparison.OrdinalIgnoreCase) ||
                                           r.Descripcion.Contains("movimiento", StringComparison.OrdinalIgnoreCase) ||
                                           r.Descripcion.Contains("movilizar", StringComparison.OrdinalIgnoreCase))
            .ShouldBeTrue();
    }

    /// <summary>
    /// Tests that PDF requirement summarization categorizes informacion requirements correctly.
    /// </summary>
    [Fact]
    public async Task SummarizeRequirementsFromTextAsync_InformacionRequirements_CategorizesCorrectly()
    {
        // Arrange
        var pdfText = @"
            REQUERIMIENTO: Se solicita información sobre movimientos
            INFORMACIÓN: Reportar transacciones del último mes
            Se requiere comunicar detalles de operaciones realizadas.
        ";

        // Act
        var result = await _summarizer.SummarizeRequirementsFromTextAsync(pdfText, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Informacion.ShouldNotBeEmpty();
        result.Value.Informacion.Count.ShouldBeGreaterThan(0);
        result.Value.Informacion.Any(r => r.Descripcion.Contains("información", StringComparison.OrdinalIgnoreCase) ||
                                          r.Descripcion.Contains("informar", StringComparison.OrdinalIgnoreCase) ||
                                          r.Descripcion.Contains("reportar", StringComparison.OrdinalIgnoreCase) ||
                                          r.Descripcion.Contains("comunicar", StringComparison.OrdinalIgnoreCase))
            .ShouldBeTrue();
    }

    /// <summary>
    /// Tests that PDF requirement summarization generates summary text.
    /// </summary>
    [Fact]
    public async Task SummarizeRequirementsFromTextAsync_ValidText_GeneratesSummaryText()
    {
        // Arrange
        var pdfText = @"
            BLOQUEO: Bloquear cuenta 1234567890
            DOCUMENTACIÓN: Presentar estados de cuenta
            INFORMACIÓN: Reportar movimientos
        ";

        // Act
        var result = await _summarizer.SummarizeRequirementsFromTextAsync(pdfText, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.SummaryText.ShouldNotBeNullOrWhiteSpace();
        result.Value.SummaryText.ShouldContain("Resumen");
    }

    /// <summary>
    /// Tests that PDF requirement summarization calculates confidence score.
    /// </summary>
    [Fact]
    public async Task SummarizeRequirementsFromTextAsync_ValidText_CalculatesConfidenceScore()
    {
        // Arrange
        var pdfText = @"
            BLOQUEO: Bloquear cuenta 1234567890
            DESBLOQUEO: Desbloquear cuenta 9876543210
            DOCUMENTACIÓN: Presentar documentación fiscal
        ";

        // Act
        var result = await _summarizer.SummarizeRequirementsFromTextAsync(pdfText, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ConfidenceScore.ShouldBeGreaterThanOrEqualTo(0);
        result.Value.ConfidenceScore.ShouldBeLessThanOrEqualTo(100);
    }

    /// <summary>
    /// Tests that PDF requirement summarization handles cancellation correctly.
    /// </summary>
    [Fact]
    public async Task SummarizeRequirementsAsync_CancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _summarizer.SummarizeRequirementsAsync(pdfContent, cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that PDF requirement summarization handles empty text gracefully.
    /// </summary>
    [Fact]
    public async Task SummarizeRequirementsFromTextAsync_EmptyText_ReturnsFailure()
    {
        // Arrange
        var pdfText = string.Empty;

        // Act
        var result = await _summarizer.SummarizeRequirementsFromTextAsync(pdfText, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("empty");
    }

    /// <summary>
    /// Tests that PDF requirement summarization handles metadata extractor failure gracefully.
    /// </summary>
    [Fact]
    public async Task SummarizeRequirementsAsync_MetadataExtractorFails_ReturnsFailure()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        _metadataExtractor.ExtractFromPdfAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtractedMetadata>.WithFailure("Extraction failed"));

        // Act
        var result = await _summarizer.SummarizeRequirementsAsync(pdfContent, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Extraction");
    }
}
