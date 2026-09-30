namespace ExxerCube.Prisma.Tests.UI.Services;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Web.UI.Models;
using ExxerCube.Prisma.Web.UI.Services;

/// <summary>
/// Unit tests for <see cref="DocumentComparisonCoordinator"/>.
/// Validates 2-way comparison orchestration, 3-way fusion delegation, and error handling.
/// </summary>
public sealed class DocumentComparisonCoordinatorTests
{
    private readonly IDocumentComparisonService _comparisonService;
    private readonly IFusionExpediente _fusionService;
    private readonly ILogger<DocumentComparisonCoordinator> _logger;
    private readonly DocumentComparisonCoordinator _sut;

    public DocumentComparisonCoordinatorTests()
    {
        _comparisonService = Substitute.For<IDocumentComparisonService>();
        _fusionService = Substitute.For<IFusionExpediente>();
        _logger = Substitute.For<ILogger<DocumentComparisonCoordinator>>();
        _sut = new DocumentComparisonCoordinator(_comparisonService, _fusionService, _logger);
    }

    [Fact]
    public async Task CompareAndFuseAsync_NullXmlExpediente_ThrowsInvalidOperationException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var viewModel = CreateViewModel(xmlExpediente: null, pdfExpediente: CreateExpediente("PDF-001"));

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.CompareAndFuseAsync(viewModel, ct));
        ex.Message.ShouldContain("XML Expediente is required");
    }

    [Fact]
    public async Task CompareAndFuseAsync_NullPdfExpediente_ThrowsInvalidOperationException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var viewModel = CreateViewModel(xmlExpediente: CreateExpediente("XML-001"), pdfExpediente: null);

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.CompareAndFuseAsync(viewModel, ct));
        ex.Message.ShouldContain("PDF Expediente is required");
    }

    [Fact]
    public async Task CompareAndFuseAsync_ValidInputs_CallsComparisonService()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var xmlExp = CreateExpediente("EXP-001");
        var pdfExp = CreateExpediente("EXP-001");
        var viewModel = CreateViewModel(xmlExp, pdfExp);

        var comparisonResult = CreateComparisonResult(matchCount: 5, totalFields: 7);
        _comparisonService.CompareExpedientesAsync(xmlExp, pdfExp, Arg.Any<CancellationToken>())
            .Returns(comparisonResult);

        var fusionResult = CreateFusionResult(0.92, NextAction.AutoProcess);
        _fusionService.FuseAsync(
                xmlExp, pdfExp, null,
                Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<FusionResult>.Success(fusionResult));

        // Act
        var result = await _sut.CompareAndFuseAsync(viewModel, ct);

        // Assert
        result.Comparison.ShouldBe(comparisonResult);
        await _comparisonService.Received(1).CompareExpedientesAsync(xmlExp, pdfExp, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompareAndFuseAsync_ValidInputs_CallsFusionService()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var xmlExp = CreateExpediente("EXP-001");
        var pdfExp = CreateExpediente("EXP-002");
        var viewModel = CreateViewModel(xmlExp, pdfExp);

        _comparisonService.CompareExpedientesAsync(Arg.Any<Expediente>(), Arg.Any<Expediente>(), Arg.Any<CancellationToken>())
            .Returns(CreateComparisonResult(3, 7));

        var fusionResult = CreateFusionResult(0.78, NextAction.ReviewRecommended);
        _fusionService.FuseAsync(
                xmlExp, pdfExp, null,
                Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<FusionResult>.Success(fusionResult));

        // Act
        var result = await _sut.CompareAndFuseAsync(viewModel, ct);

        // Assert
        result.Fusion.ShouldNotBeNull();
        result.Fusion.Confidence.Value.ShouldBe(0.78);
        result.Fusion.NextAction.ShouldBe(NextAction.ReviewRecommended);
        await _fusionService.Received(1).FuseAsync(
            xmlExp, pdfExp, null,
            Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompareAndFuseAsync_FusionFails_ReturnsFusionNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var xmlExp = CreateExpediente("EXP-001");
        var pdfExp = CreateExpediente("EXP-001");
        var viewModel = CreateViewModel(xmlExp, pdfExp);

        _comparisonService.CompareExpedientesAsync(Arg.Any<Expediente>(), Arg.Any<Expediente>(), Arg.Any<CancellationToken>())
            .Returns(CreateComparisonResult(5, 7));

        _fusionService.FuseAsync(
                Arg.Any<Expediente>(), Arg.Any<Expediente>(), Arg.Any<Expediente?>(),
                Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<FusionResult>.WithFailure("Fusion engine error"));

        // Act
        var result = await _sut.CompareAndFuseAsync(viewModel, ct);

        // Assert
        result.Comparison.ShouldNotBeNull();
        result.Fusion.ShouldBeNull();
    }

    [Fact]
    public async Task CompareAndFuseAsync_UsesExistingMetadata_WhenAvailable()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var xmlExp = CreateExpediente("EXP-001");
        var pdfExp = CreateExpediente("EXP-001");
        var viewModel = CreateViewModel(xmlExp, pdfExp);

        var xmlMeta = new ExtractionMetadata
        {
            Source = SourceType.XML_HandFilled,
            TotalFieldsExtracted = 10,
            RegexMatches = 5
        };
        viewModel.XmlState.Metadata = xmlMeta;

        var pdfMeta = new ExtractionMetadata
        {
            Source = SourceType.PDF_OCR_CNBV,
            MeanConfidence = 0.85,
            TotalFieldsExtracted = 8
        };
        viewModel.PdfState.Metadata = pdfMeta;

        _comparisonService.CompareExpedientesAsync(Arg.Any<Expediente>(), Arg.Any<Expediente>(), Arg.Any<CancellationToken>())
            .Returns(CreateComparisonResult(5, 7));

        _fusionService.FuseAsync(
                Arg.Any<Expediente>(), Arg.Any<Expediente>(), Arg.Any<Expediente?>(),
                Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<FusionResult>.Success(CreateFusionResult(0.90, NextAction.AutoProcess)));

        // Act
        await _sut.CompareAndFuseAsync(viewModel, ct);

        // Assert — verify the actual metadata was passed (not default-created)
        await _fusionService.Received(1).FuseAsync(
            xmlExp, pdfExp, null,
            xmlMeta, pdfMeta, Arg.Any<ExtractionMetadata>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompareAndFuseAsync_ComparisonThrows_PropagatesException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var xmlExp = CreateExpediente("EXP-001");
        var pdfExp = CreateExpediente("EXP-001");
        var viewModel = CreateViewModel(xmlExp, pdfExp);

        _comparisonService.CompareExpedientesAsync(Arg.Any<Expediente>(), Arg.Any<Expediente>(), Arg.Any<CancellationToken>())
            .Returns<ComparisonResult>(_ => throw new TimeoutException("Comparison timed out"));

        // Act & Assert
        await Should.ThrowAsync<TimeoutException>(
            () => _sut.CompareAndFuseAsync(viewModel, ct));
    }

    [Fact]
    public async Task CompareAndFuseAsync_DocxMetadata_IsPlaceholderWithZeroFields()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var xmlExp = CreateExpediente("EXP-001");
        var pdfExp = CreateExpediente("EXP-001");
        var viewModel = CreateViewModel(xmlExp, pdfExp);

        _comparisonService.CompareExpedientesAsync(Arg.Any<Expediente>(), Arg.Any<Expediente>(), Arg.Any<CancellationToken>())
            .Returns(CreateComparisonResult(5, 7));

        ExtractionMetadata? capturedDocxMeta = null;
        _fusionService.FuseAsync(
                Arg.Any<Expediente>(), Arg.Any<Expediente>(), Arg.Any<Expediente?>(),
                Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
                Arg.Do<ExtractionMetadata>(m => capturedDocxMeta = m),
                Arg.Any<CancellationToken>())
            .Returns(Result<FusionResult>.Success(CreateFusionResult(0.90, NextAction.AutoProcess)));

        // Act
        await _sut.CompareAndFuseAsync(viewModel, ct);

        // Assert — DOCX metadata is placeholder with no extracted fields
        capturedDocxMeta.ShouldNotBeNull();
        capturedDocxMeta.Source.ShouldBe(SourceType.DOCX_OCR_Authority);
        capturedDocxMeta.TotalFieldsExtracted.ShouldBe(0);
    }

    [Fact]
    public async Task CompareAndFuseAsync_HighConfidenceFusion_ReturnsAutoProcess()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var xmlExp = CreateExpediente("EXP-001");
        var pdfExp = CreateExpediente("EXP-001");
        var viewModel = CreateViewModel(xmlExp, pdfExp);

        _comparisonService.CompareExpedientesAsync(Arg.Any<Expediente>(), Arg.Any<Expediente>(), Arg.Any<CancellationToken>())
            .Returns(CreateComparisonResult(7, 7));

        var fusionResult = CreateFusionResult(0.95, NextAction.AutoProcess);
        _fusionService.FuseAsync(
                Arg.Any<Expediente>(), Arg.Any<Expediente>(), Arg.Any<Expediente?>(),
                Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<FusionResult>.Success(fusionResult));

        // Act
        var result = await _sut.CompareAndFuseAsync(viewModel, ct);

        // Assert
        result.Comparison.MatchCount.ShouldBe(7);
        result.Comparison.TotalFields.ShouldBe(7);
        result.Fusion.ShouldNotBeNull();
        result.Fusion.NextAction.ShouldBe(NextAction.AutoProcess);
        result.Fusion.Confidence.Value.ShouldBe(0.95);
    }

    // --- Helpers ---

    private static DocumentProcessingViewModel CreateViewModel(
        Expediente? xmlExpediente,
        Expediente? pdfExpediente)
    {
        return new DocumentProcessingViewModel
        {
            XmlState = { Expediente = xmlExpediente, FieldCount = 5 },
            PdfState = { Expediente = pdfExpediente }
        };
    }

    private static Expediente CreateExpediente(string numero)
    {
        return new Expediente
        {
            NumeroExpediente = numero,
            NumeroOficio = "OF-2025-001",
            AutoridadNombre = "CNBV",
            FundamentoLegal = "Art. 115",
            Referencia1 = "Causa Test",
            Referencia2 = "Accion Test"
        };
    }

    private static ComparisonResult CreateComparisonResult(int matchCount, int totalFields)
    {
        return new ComparisonResult
        {
            MatchCount = matchCount,
            TotalFields = totalFields,
            OverallSimilarity = totalFields > 0 ? matchCount / (float)totalFields : 0f
        };
    }

    private static FusionResult CreateFusionResult(double confidence, NextAction nextAction)
    {
        return new FusionResult
        {
            Confidence = Confidence.FromFusion(confidence),
            NextAction = nextAction,
            FusedExpediente = CreateExpediente("FUSED-001")
        };
    }
}
