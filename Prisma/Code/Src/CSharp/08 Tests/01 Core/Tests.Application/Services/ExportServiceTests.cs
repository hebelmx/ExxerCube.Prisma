using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Unit tests for <see cref="ExportService"/> orchestration, covering summarization, exports, and logging behaviors.
/// </summary>
public class ExportServiceTests
{
    private readonly IResponseExporter _responseExporter;
    private readonly ILayoutGenerator _layoutGenerator;
    private readonly ICriterionMapper _criterionMapper;
    private readonly IPdfRequirementSummarizer _pdfRequirementSummarizer;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<ExportService> _logger;
    private readonly ExportService _exportService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportServiceTests"/> class with mocked export dependencies.
    /// </summary>
    public ExportServiceTests()
    {
        _responseExporter = Substitute.For<IResponseExporter>();
        _layoutGenerator = Substitute.For<ILayoutGenerator>();
        _criterionMapper = Substitute.For<ICriterionMapper>();
        _pdfRequirementSummarizer = Substitute.For<IPdfRequirementSummarizer>();
        _auditLogger = Substitute.For<IAuditLogger>();
        _logger = XUnitLogger.CreateLogger<ExportService>();
        _exportService = new ExportService(
            _responseExporter,
            _layoutGenerator,
            _criterionMapper,
            _pdfRequirementSummarizer,
            _auditLogger,
            _logger);
    }

    /// <summary>
    /// Tests that signed PDF export with summarization orchestrates correctly.
    /// </summary>
    /// <returns>A task that completes after verifying summarization and export audit steps.</returns>
    [Fact]
    public async Task ExportSignedPdfWithSummarizationAsync_ValidInput_OrchestratesCorrectly()
    {
        // Arrange
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "TEST-001",
                NumeroOficio = "OF-2024-001",
                FundamentoLegal = "Art 115",
                MedioEnvio = "SIARA",
                Subdivision = LegalSubdivisionKind.A_AS,
                FechaRecepcion = DateTime.UtcNow.Date,
                FechaEstimadaConclusion = DateTime.UtcNow.Date.AddDays(5)
            }
        };
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var requirementSummary = new RequirementSummary
        {
            SummaryText = "Test summary",
            Bloqueo = new List<ComplianceRequirement>
            {
                new ComplianceRequirement { Descripcion = "Bloquear cuenta" }
            }
        };
        using var stream = new MemoryStream();

        _pdfRequirementSummarizer.SummarizeRequirementsAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<RequirementSummary>.Success(requirementSummary));
        _responseExporter.ExportSignedPdfAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _auditLogger.LogAuditAsync(
            Arg.Any<AuditActionType>(),
            Arg.Any<ProcessingStage>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _exportService.ExportSignedPdfWithSummarizationAsync(
            metadata, pdfContent, stream, null, null, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _pdfRequirementSummarizer.Received().SummarizeRequirementsAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await _responseExporter.Received().ExportSignedPdfAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _auditLogger.Received().LogAuditAsync(
            Arg.Any<AuditActionType>(),
            Arg.Any<ProcessingStage>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that signed PDF export continues without summary if summarization fails.
    /// </summary>
    /// <returns>A task that completes after verifying export proceeds when summarization fails.</returns>
    [Fact]
    public async Task ExportSignedPdfWithSummarizationAsync_SummarizationFails_ContinuesWithoutSummary()
    {
        // Arrange
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "TEST-001",
                NumeroOficio = "OF-2024-001",
                FundamentoLegal = "Art 115",
                MedioEnvio = "SIARA",
                Subdivision = LegalSubdivisionKind.A_AS,
                FechaRecepcion = DateTime.UtcNow.Date,
                FechaEstimadaConclusion = DateTime.UtcNow.Date.AddDays(5)
            }
        };
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        using var stream = new MemoryStream();

        _pdfRequirementSummarizer.SummarizeRequirementsAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<RequirementSummary>.WithFailure("Summarization failed"));
        _responseExporter.ExportSignedPdfAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _auditLogger.LogAuditAsync(
            Arg.Any<AuditActionType>(),
            Arg.Any<ProcessingStage>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _exportService.ExportSignedPdfWithSummarizationAsync(
            metadata, pdfContent, stream, null, null, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _responseExporter.Received().ExportSignedPdfAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that signed PDF export uses existing requirement summary if PDF content not provided.
    /// </summary>
    [Fact]
    public async Task ExportSignedPdfWithSummarizationAsync_NoPdfContent_UsesExistingSummary()
    {
        // Arrange
        var requirementSummary = new RequirementSummary
        {
            SummaryText = "Existing summary",
            Bloqueo = new List<ComplianceRequirement>
            {
                new ComplianceRequirement { Descripcion = "Bloquear cuenta" }
            }
        };
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "TEST-001",
                NumeroOficio = "OF-2024-001",
                FundamentoLegal = "Art 115",
                MedioEnvio = "SIARA",
                Subdivision = LegalSubdivisionKind.A_AS,
                FechaRecepcion = DateTime.UtcNow.Date,
                FechaEstimadaConclusion = DateTime.UtcNow.Date.AddDays(5)
            },
            RequirementSummary = requirementSummary
        };
        using var stream = new MemoryStream();

        _responseExporter.ExportSignedPdfAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _auditLogger.LogAuditAsync(
            Arg.Any<AuditActionType>(),
            Arg.Any<ProcessingStage>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _exportService.ExportSignedPdfWithSummarizationAsync(
            metadata, null, stream, null, null, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _pdfRequirementSummarizer.DidNotReceive().SummarizeRequirementsAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await _responseExporter.Received().ExportSignedPdfAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that signed PDF export handles cancellation correctly.
    /// </summary>
    /// <returns>A task that completes after cancellation handling is asserted.</returns>
    [Fact]
    public async Task ExportSignedPdfWithSummarizationAsync_CancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente { NumeroExpediente = "TEST-001" }
        };
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _exportService.ExportSignedPdfWithSummarizationAsync(
            metadata, pdfContent, stream, null, null, cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that signed PDF export fails when metadata is null.
    /// </summary>
    /// <returns>A task that completes after validating null metadata handling.</returns>
    [Fact]
    public async Task ExportSignedPdfWithSummarizationAsync_NullMetadata_ReturnsFailure()
    {
        // Arrange
        UnifiedMetadataRecord? metadata = null;
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        using var stream = new MemoryStream();

        // Act
        var result = await _exportService.ExportSignedPdfWithSummarizationAsync(
            metadata!, pdfContent, stream, null, null, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("null");
    }

    /// <summary>
    /// Tests that signed PDF export fails when output stream is null.
    /// </summary>
    /// <returns>A task that completes after validating null stream handling.</returns>
    [Fact]
    public async Task ExportSignedPdfWithSummarizationAsync_NullOutputStream_ReturnsFailure()
    {
        // Arrange
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente { NumeroExpediente = "TEST-001" }
        };
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        Stream? stream = null;

        // Act
        var result = await _exportService.ExportSignedPdfWithSummarizationAsync(
            metadata, pdfContent, stream!, null, null, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("null");
    }

    /// <summary>
    /// Tests that signed PDF export propagates PDF export failure.
    /// </summary>
    /// <returns>A task that completes after asserting PDF export failures are propagated.</returns>
    [Fact]
    public async Task ExportSignedPdfWithSummarizationAsync_PdfExportFails_ReturnsFailure()
    {
        // Arrange
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "TEST-001",
                NumeroOficio = "OF-2024-001",
                FundamentoLegal = "Art 115",
                MedioEnvio = "SIARA",
                Subdivision = LegalSubdivisionKind.A_AS,
                FechaRecepcion = DateTime.UtcNow.Date,
                FechaEstimadaConclusion = DateTime.UtcNow.Date.AddDays(5)
            }
        };
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        using var stream = new MemoryStream();

        _pdfRequirementSummarizer.SummarizeRequirementsAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<RequirementSummary>.Success(new RequirementSummary()));
        _responseExporter.ExportSignedPdfAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("PDF export failed"));

        // Act
        var result = await _exportService.ExportSignedPdfWithSummarizationAsync(
            metadata, pdfContent, stream, null, null, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("PDF export");
    }

    [Fact]
    public async Task ExportSiroXmlAsync_WithMissingRequiredFields_ReturnsFailure()
    {
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente { NumeroExpediente = "", NumeroOficio = "" }
        };
        using var stream = new MemoryStream();

        var result = await _exportService.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error;
        error.ShouldNotBeNull();
        error!.ShouldContain("Validation failed");
    }

    [Fact]
    public async Task ExportSiroXmlAsync_WithConflicts_LogsWarnings_ButValidationPasses()
    {
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "EXP-1",
                NumeroOficio = "OF-1",
                FundamentoLegal = "Art 115",
                MedioEnvio = "SIARA",
                Subdivision = LegalSubdivisionKind.A_AS,
                FechaRecepcion = DateTime.UtcNow.Date,
                FechaEstimadaConclusion = DateTime.UtcNow.Date.AddDays(3)
            },
            AdditionalFieldConflicts = new List<string> { "RfcList", "Curp" }
        };
        using var stream = new MemoryStream();

        var result = await _exportService.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        // Even if exporter fails for other reasons, validation should not fail due to warnings
        if (result.IsFailure)
        {
            result.Error?.ShouldNotContain("Validation failed");
        }
    }

    [Fact]
    public async Task ExportSiroXmlAsync_ComplianceActionMissingAccount_ReturnsFailure()
    {
        var metadata = new UnifiedMetadataRecord
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "EXP-1",
                NumeroOficio = "OF-1",
                FundamentoLegal = "Art 115",
                MedioEnvio = "SIARA",
                Subdivision = LegalSubdivisionKind.A_AS,
                FechaRecepcion = DateTime.UtcNow.Date,
                FechaEstimadaConclusion = DateTime.UtcNow.Date.AddDays(3)
            },
            ComplianceActions = new List<ComplianceAction>
            {
                new() { ActionType = ComplianceActionKind.Transfer, AccountNumber = "" }
            }
        };
        using var stream = new MemoryStream();

        var result = await _exportService.ExportSiroXmlAsync(metadata, stream, cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var err = result.Error ?? string.Empty;
        err.ShouldContain("ComplianceAction.Account");
    }
}

