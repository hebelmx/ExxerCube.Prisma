namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Performance tests for <see cref="ExportService"/> to verify NFR8 and NFR9 timing and throughput requirements.
/// </summary>
public class ExportServicePerformanceTests
{
    private readonly IResponseExporter _responseExporter;
    private readonly ILayoutGenerator _layoutGenerator;
    private readonly ICriterionMapper _criterionMapper;
    private readonly IPdfRequirementSummarizer _pdfRequirementSummarizer;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<ExportService> _logger;
    private readonly ExportService _exportService;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportServicePerformanceTests"/> class with mocked exporters and loggers.
    /// </summary>
    public ExportServicePerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _responseExporter = Substitute.For<IResponseExporter>();
        _layoutGenerator = Substitute.For<ILayoutGenerator>();
        _criterionMapper = Substitute.For<ICriterionMapper>();
        _pdfRequirementSummarizer = Substitute.For<IPdfRequirementSummarizer>();
        _auditLogger = Substitute.For<IAuditLogger>();
        _logger = XUnitLogger.CreateLogger<ExportService>(output);
        _exportService = new ExportService(
            _responseExporter,
            _layoutGenerator,
            _criterionMapper,
            _pdfRequirementSummarizer,
            _auditLogger,
            _logger);

        // Setup successful responses
        _responseExporter.ExportSiroXmlAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _layoutGenerator.GenerateExcelLayoutAsync(Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
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
    }

    /// <summary>
    /// Creates a representative unified metadata record used in performance scenarios.
    /// </summary>
    private static UnifiedMetadataRecord CreateTypicalMetadata()
    {
        return new UnifiedMetadataRecord
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "EXP-2024-001",
                NumeroOficio = "OF-2024-001",
                FundamentoLegal = "Art 115",
                MedioEnvio = "SIARA",
                Subdivision = LegalSubdivisionKind.A_AS,
                FechaRecepcion = DateTime.UtcNow.AddDays(-5),
                FechaEstimadaConclusion = DateTime.UtcNow.AddDays(5)
            },
            Persona = new Persona
            {
                Rfc = "ABCD123456EF7",
                Nombre = "Juan",
                Paterno = "Pérez",
                Materno = "García"
            },
            ComplianceActions = new List<ComplianceAction>
            {
                new ComplianceAction
                {
                    ActionType = ComplianceActionKind.Block,
                    AccountNumber = "1234567890",
                    RequerimientoOrigen = "Bloquear cuenta bancaria",
                    LegalBasis = "Artículo 123 de la Ley",
                    DueDate = DateTime.UtcNow.AddDays(10)
                }
            }
        };
    }

    /// <summary>
    /// Tests that ExportSiroXmlAsync completes within 5 seconds (NFR8).
    /// </summary>
    /// <returns>A task that completes after timing assertions are evaluated.</returns>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ExportSiroXmlAsync_CompletesWithin5Seconds_NFR8()
    {
        // Arrange
        var metadata = CreateTypicalMetadata();
        using var stream = new MemoryStream();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _exportService.ExportSiroXmlAsync(
            metadata,
            stream,
            null,
            null,
            CancellationToken.None);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000,
            $"ExportSiroXmlAsync took {stopwatch.ElapsedMilliseconds}ms, exceeding NFR8 target of <5s (5000ms)");

        _output.WriteLine($"ExportSiroXmlAsync completed in {stopwatch.ElapsedMilliseconds}ms (NFR8 target: <5000ms)");
    }

    /// <summary>
    /// Tests that GenerateExcelLayoutAsync completes within 3 seconds (NFR9).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task GenerateExcelLayoutAsync_CompletesWithin3Seconds_NFR9()
    {
        // Arrange
        var metadata = CreateTypicalMetadata();
        using var stream = new MemoryStream();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _exportService.GenerateExcelLayoutAsync(
            metadata,
            stream,
            null,
            null,
            CancellationToken.None);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(3000,
            $"GenerateExcelLayoutAsync took {stopwatch.ElapsedMilliseconds}ms, exceeding NFR9 target of <3s (3000ms)");

        _output.WriteLine($"GenerateExcelLayoutAsync completed in {stopwatch.ElapsedMilliseconds}ms (NFR9 target: <3000ms)");
    }

    /// <summary>
    /// Tests that export operations don't block other document processing operations (IV3).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ExportOperations_DoNotBlockDocumentProcessing_IV3()
    {
        // Arrange: Simulate concurrent export and document processing
        var metadata = CreateTypicalMetadata();
        var exportTasks = new List<Task<Result>>();
        var processingTasks = new List<Task>();

        // Act: Start multiple export operations concurrently with simulated document processing
        var stopwatch = Stopwatch.StartNew();

        // Start 5 export operations
        for (int i = 0; i < 5; i++)
        {
            using var stream = new MemoryStream();
            exportTasks.Add(_exportService.ExportSiroXmlAsync(metadata, stream, null, null, CancellationToken.None));
        }

        // Simulate document processing tasks (should not be blocked)
        for (int i = 0; i < 10; i++)
        {
            processingTasks.Add(Task.Delay(50, CancellationToken.None));
        }

        // Wait for all tasks
        await Task.WhenAll(exportTasks);
        await Task.WhenAll(processingTasks);
        stopwatch.Stop();

        // Assert: All exports should succeed
        exportTasks.ShouldAllBe(t => t.Result.IsSuccess);

        // Performance: Total time should be reasonable (not blocked by exports)
        // If exports are truly async and non-blocking, total time should be close to processing time
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(10000,
            $"Export operations significantly blocked processing: {stopwatch.ElapsedMilliseconds}ms");

        _output.WriteLine($"Concurrent export and processing completed in {stopwatch.ElapsedMilliseconds}ms (IV3 verification)");
    }

    /// <summary>
    /// Tests that bulk export operations perform efficiently.
    /// </summary>
    /// <returns>A task that completes after bulk export performance assertions are evaluated.</returns>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task BulkExportOperations_PerformEfficiently()
    {
        // Arrange: Create multiple export operations
        var metadata = CreateTypicalMetadata();
        var exportCount = 10;

        // Act: Execute multiple exports
        var stopwatch = Stopwatch.StartNew();
        var exportTasks = new List<Task<Result>>();

        for (int i = 0; i < exportCount; i++)
        {
            using var stream = new MemoryStream();
            exportTasks.Add(_exportService.ExportSiroXmlAsync(metadata, stream, null, null, CancellationToken.None));
        }

        var results = await Task.WhenAll(exportTasks);
        stopwatch.Stop();

        // Assert: All exports should succeed
        results.ShouldAllBe(r => r.IsSuccess);

        // Performance: Average time per export should be reasonable
        var avgTimePerExport = stopwatch.ElapsedMilliseconds / (double)exportCount;
        avgTimePerExport.ShouldBeLessThan(1000,
            $"Average export time: {avgTimePerExport}ms per export, exceeding 1000ms target");

        _output.WriteLine($"Bulk export ({exportCount} exports) completed in {stopwatch.ElapsedMilliseconds}ms (avg: {avgTimePerExport:F2}ms per export)");
    }
}


