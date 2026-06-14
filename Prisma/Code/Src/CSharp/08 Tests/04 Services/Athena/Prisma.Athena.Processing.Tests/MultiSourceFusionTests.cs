using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Tests for MVP-PATH 2.1 Phase C: multi-source (XML + PDF + DOCX) fusion in
/// <see cref="ExtractionOrchestrator"/> and CaseFiles path resolution in
/// <see cref="ExtractionOrchestrator"/>.
///
/// Design: when a <see cref="DocumentDownloadedEvent"/> carries companion
/// <see cref="CaseFileReference"/> entries, the orchestrator extracts each present source and
/// feeds all three expedientes into <see cref="IFusionExpediente.FuseAsync"/>. When
/// <see cref="DocumentDownloadedEvent.CaseFiles"/> is empty the orchestrator behaves exactly as
/// before (PDF-only, xml and docx expedientes are null) — backward compatibility.
/// </summary>
public sealed class MultiSourceFusionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="DocumentDownloadedEvent"/> with a PDF primary file and optional
    /// XML and DOCX companion <see cref="CaseFileReference"/> entries.
    /// </summary>
    private static DocumentDownloadedEvent CreateCaseEvent(
        bool withXml = false,
        bool withDocx = false,
        string pdfPath = "C:/storage/2026/06/case.pdf",
        string xmlPath = "C:/storage/2026/06/case.xml",
        string docxPath = "C:/storage/2026/06/case.docx")
    {
        var caseFiles = new List<CaseFileReference>
        {
            // The primary PDF is also listed in CaseFiles for completeness.
            new() { RelativePath = pdfPath, Format = FileFormat.Pdf },
        };

        if (withXml)
        {
            caseFiles.Add(new CaseFileReference { RelativePath = xmlPath, Format = FileFormat.Xml });
        }

        if (withDocx)
        {
            caseFiles.Add(new CaseFileReference { RelativePath = docxPath, Format = FileFormat.Docx });
        }

        return new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            FileName = pdfPath,
            Source = "SIARA",
            Format = FileFormat.Pdf,
            CaseFiles = caseFiles,
        };
    }

    /// <summary>
    /// Creates mocked quality+OCR services that unconditionally succeed with a Pristine image
    /// and non-empty OCR text, mirroring the pattern in <see cref="ExtractionPipelineServiceTests"/>.
    /// </summary>
    private static (IFileLoader, IImageQualityAnalyzer, IOcrExecutor) CreatePdfMocks()
    {
        var fileLoader = Substitute.For<IFileLoader>();
        fileLoader.LoadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImageData>.Success(new ImageData(new byte[] { 1, 2, 3 }, "case.pdf")));

        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        qualityAnalyzer.AnalyzeAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageQualityAssessment>.Success(new ImageQualityAssessment
            {
                QualityLevel = ImageQualityLevel.Pristine,
                Confidence = 0.95f,
            }));

        var ocrExecutor = Substitute.For<IOcrExecutor>();
        ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<ExxerCube.Prisma.Domain.Models.OCRConfig>())
            .Returns(Result<OCRResult>.Success(
                new OCRResult("Expediente: EXP-001 NumeroOficio: OFF-001", 90f, 91f, new List<float> { 90, 91 }, "spa")));

        return (fileLoader, qualityAnalyzer, ocrExecutor);
    }

    /// <summary>
    /// Creates a mock <see cref="IFieldExtractor{T}"/> that returns a non-empty
    /// <see cref="ExtractedFields"/> (with the given NumeroExpediente) for any source input.
    /// </summary>
    private static IFieldExtractor<T> CreateExtractorMock<T>(string numeroExpediente)
        where T : class
    {
        var mock = Substitute.For<IFieldExtractor<T>>();
        mock.ExtractFieldsAsync(Arg.Any<T>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields
            {
                Expediente = numeroExpediente,
                Causa = "Test causa",
                AccionSolicitada = "Test accion",
            }));
        return mock;
    }

    /// <summary>
    /// Creates a fusion mock that captures the expedientes passed to
    /// <see cref="IFusionExpediente.FuseAsync"/> and returns a successful empty result.
    /// </summary>
    private static IFusionExpediente CreateFusionCapture(
        out Expediente?[] captured)
    {
        var capturedArr = new Expediente?[3]; // [0]=xml, [1]=pdf, [2]=docx
        var fusionMock = Substitute.For<IFusionExpediente>();
        fusionMock
            .FuseAsync(
                Arg.Any<Expediente?>(),
                Arg.Any<Expediente?>(),
                Arg.Any<Expediente?>(),
                Arg.Any<ExtractionMetadata>(),
                Arg.Any<ExtractionMetadata>(),
                Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedArr[0] = callInfo.ArgAt<Expediente?>(0); // xmlExpediente
                capturedArr[1] = callInfo.ArgAt<Expediente?>(1); // pdfExpediente
                capturedArr[2] = callInfo.ArgAt<Expediente?>(2); // docxExpediente
                return Result<FusionResult>.Success(new FusionResult
                {
                    OverallConfidence = 0.90,
                    ConflictingFields = new List<string>(),
                    FusedExpediente = new Expediente { NumeroExpediente = "FUSED-001" },
                });
            });
        captured = capturedArr;
        return fusionMock;
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    /// <summary>
    /// DoD assertion: when the event carries XML and DOCX CaseFileReferences and both extractors
    /// are configured, FuseAsync is called with non-null xmlExpediente, non-null pdfExpediente,
    /// and non-null docxExpediente.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithXmlAndDocxCaseFiles_FusesAllThreeSources()
    {
        // Arrange
        var (fileLoader, qualityAnalyzer, ocrExecutor) = CreatePdfMocks();
        var fusionService = CreateFusionCapture(out var captured);

        // A txt extractor is needed so the PDF OCR path produces a non-null pdfExpediente.
        var txtExtractor = CreateExtractorMock<TxtSource>("EXP-PDF");
        var xmlExtractor = CreateExtractorMock<XmlSource>("EXP-XML");
        var docxExtractor = CreateExtractorMock<DocxSource>("EXP-DOCX");

        var orchestrator = new ExtractionOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ExtractionOrchestrator>.Instance,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            fileLoader: fileLoader,
            txtFieldExtractor: txtExtractor,
            xmlFieldExtractor: xmlExtractor,
            docxFieldExtractor: docxExtractor);

        var downloadEvent = CreateCaseEvent(withXml: true, withDocx: true);

        // Act
        var result = await orchestrator.ExtractAsync(downloadEvent, Ct);

        // Assert: pipeline completed all 3 stages.
        result.QualityRejected.ShouldBeFalse();
        result.StagesCompleted.ShouldBe(3);
        result.FusionResult.ShouldNotBeNull();

        // Assert: FuseAsync was called with all three non-null expedientes.
        await fusionService.Received(1).FuseAsync(
            Arg.Is<Expediente?>(e => e != null),   // xmlExpediente
            Arg.Is<Expediente?>(e => e != null),   // pdfExpediente
            Arg.Is<Expediente?>(e => e != null),   // docxExpediente
            Arg.Any<ExtractionMetadata>(),
            Arg.Any<ExtractionMetadata>(),
            Arg.Any<ExtractionMetadata>(),
            Arg.Any<CancellationToken>());

        // Verify the captured values match the extracted expedientes.
        captured[0].ShouldNotBeNull("XML expediente should be non-null");
        captured[1].ShouldNotBeNull("PDF expediente should be non-null");
        captured[2].ShouldNotBeNull("DOCX expediente should be non-null");

        captured[0]!.NumeroExpediente.ShouldBe("EXP-XML");
        captured[2]!.NumeroExpediente.ShouldBe("EXP-DOCX");
    }

    /// <summary>
    /// Backward-compat assertion: when CaseFiles is empty (legacy single-file event), FuseAsync is
    /// called with null xmlExpediente and null docxExpediente (PDF-only fuse — exactly as before).
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithEmptyCaseFiles_FusesPdfOnly()
    {
        // Arrange
        var (fileLoader, qualityAnalyzer, ocrExecutor) = CreatePdfMocks();
        var fusionService = CreateFusionCapture(out var captured);
        var txtExtractor = CreateExtractorMock<TxtSource>("EXP-PDF");

        // XML + DOCX extractors ARE wired — but the event has no companion CaseFiles,
        // so neither should be called.
        var xmlExtractor = Substitute.For<IFieldExtractor<XmlSource>>();
        var docxExtractor = Substitute.For<IFieldExtractor<DocxSource>>();

        var orchestrator = new ExtractionOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ExtractionOrchestrator>.Instance,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            fileLoader: fileLoader,
            txtFieldExtractor: txtExtractor,
            xmlFieldExtractor: xmlExtractor,
            docxFieldExtractor: docxExtractor);

        // Empty CaseFiles (legacy single-file event).
        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "case.pdf",
            Source = "SIARA",
            Format = FileFormat.Pdf,
            // CaseFiles defaults to Array.Empty — no companion files.
        };

        // Act
        await orchestrator.ExtractAsync(downloadEvent, Ct);

        // Assert: companion extractors were NOT called.
        await xmlExtractor.DidNotReceive()
            .ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>());
        await docxExtractor.DidNotReceive()
            .ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>());

        // Assert: fusion was called with null xml and docx.
        await fusionService.Received(1).FuseAsync(
            Arg.Is<Expediente?>(e => e == null),   // xmlExpediente — null
            Arg.Is<Expediente?>(e => e != null),   // pdfExpediente — present
            Arg.Is<Expediente?>(e => e == null),   // docxExpediente — null
            Arg.Any<ExtractionMetadata>(),
            Arg.Any<ExtractionMetadata>(),
            Arg.Any<ExtractionMetadata>(),
            Arg.Any<CancellationToken>());

        captured[0].ShouldBeNull("XML expediente must be null for legacy single-file event");
        captured[2].ShouldBeNull("DOCX expediente must be null for legacy single-file event");
    }

    /// <summary>
    /// Degradation assertion: when the XML extractor returns failure, fusion degrades gracefully
    /// (xmlExpediente = null, DOCX and PDF still passed if present) — no exception.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_XmlExtractionFails_DegradesToPdfPlusDocx()
    {
        // Arrange
        var (fileLoader, qualityAnalyzer, ocrExecutor) = CreatePdfMocks();
        var fusionService = CreateFusionCapture(out var captured);
        var txtExtractor = CreateExtractorMock<TxtSource>("EXP-PDF");
        var docxExtractor = CreateExtractorMock<DocxSource>("EXP-DOCX");

        // XML extractor fails.
        var xmlExtractor = Substitute.For<IFieldExtractor<XmlSource>>();
        xmlExtractor.ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.WithFailure("XML parse error"));

        var orchestrator = new ExtractionOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ExtractionOrchestrator>.Instance,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            fileLoader: fileLoader,
            txtFieldExtractor: txtExtractor,
            xmlFieldExtractor: xmlExtractor,
            docxFieldExtractor: docxExtractor);

        var downloadEvent = CreateCaseEvent(withXml: true, withDocx: true);

        // Act — must NOT throw.
        var result = await orchestrator.ExtractAsync(downloadEvent, Ct);

        // Assert: pipeline still completed stage 3.
        result.QualityRejected.ShouldBeFalse();
        result.FusionResult.ShouldNotBeNull();

        // XML degraded to null; PDF and DOCX are present.
        captured[0].ShouldBeNull("XML expediente must be null when XML extraction fails");
        captured[1].ShouldNotBeNull("PDF expediente must be non-null");
        captured[2].ShouldNotBeNull("DOCX expediente must be non-null");
    }

    /// <summary>
    /// Verifies that when only an XML companion file is present (no DOCX) only the XML and PDF
    /// expedientes are non-null; docxExpediente is null.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithXmlOnlyCompanion_FusesXmlAndPdf()
    {
        // Arrange
        var (fileLoader, qualityAnalyzer, ocrExecutor) = CreatePdfMocks();
        var fusionService = CreateFusionCapture(out var captured);
        var txtExtractor = CreateExtractorMock<TxtSource>("EXP-PDF");
        var xmlExtractor = CreateExtractorMock<XmlSource>("EXP-XML");
        var docxExtractor = Substitute.For<IFieldExtractor<DocxSource>>();

        var orchestrator = new ExtractionOrchestrator(
            Substitute.For<IEventPublisher>(),
            NullLogger<ExtractionOrchestrator>.Instance,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            fileLoader: fileLoader,
            txtFieldExtractor: txtExtractor,
            xmlFieldExtractor: xmlExtractor,
            docxFieldExtractor: docxExtractor);

        var downloadEvent = CreateCaseEvent(withXml: true, withDocx: false);

        // Act
        await orchestrator.ExtractAsync(downloadEvent, Ct);

        // Assert
        captured[0].ShouldNotBeNull("XML expediente must be non-null when XML companion present");
        captured[1].ShouldNotBeNull("PDF expediente must be non-null");
        captured[2].ShouldBeNull("DOCX expediente must be null — no DOCX companion file");

        await docxExtractor.DidNotReceive()
            .ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>());
    }
}
