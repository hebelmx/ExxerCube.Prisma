namespace ExxerCube.Prisma.Tests.Infrastructure.Metrics;

/// <summary>
/// Unit tests for <see cref="ProcessingMetricsService"/>.
/// </summary>
public class ProcessingMetricsServiceTests
{
    private readonly ILogger<ProcessingMetricsService> _logger;
    private readonly ProcessingMetricsService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingMetricsServiceTests"/> class.
    /// </summary>
    public ProcessingMetricsServiceTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<ProcessingMetricsService>(output);
        _service = new ProcessingMetricsService(_logger, maxConcurrency: 5);
    }

    /// <summary>
    /// Tests that StartProcessingAsync creates a processing context and increments active count.
    /// </summary>
    [Fact]
    public async Task StartProcessingAsync_CreatesContextAndIncrementsActiveCount()
    {
        // Arrange
        var documentId = "test-doc-1";
        var sourcePath = "test/path/document.pdf";

        // Act
        var context = await _service.StartProcessingAsync(documentId, sourcePath);

        // Assert
        context.ShouldNotBeNull();
        context.DocumentId.ShouldBe(documentId);
        context.SourcePath.ShouldBe(sourcePath);
        context.Stopwatch.ShouldNotBeNull();
        context.Stopwatch.IsRunning.ShouldBeTrue();
        _service.ActiveProcessingCount.ShouldBe(1);
        _service.MaxConcurrency.ShouldBe(5);
    }

    /// <summary>
    /// Tests that CompleteProcessingAsync records metrics and decrements active count.
    /// </summary>
    [Fact]
    public async Task CompleteProcessingAsync_RecordsMetricsAndDecrementsActiveCount()
    {
        // Arrange
        var documentId = "test-doc-2";
        var sourcePath = "test/path/document2.pdf";
        var context = await _service.StartProcessingAsync(documentId, sourcePath);
        await Task.Delay(100, TestContext.Current.CancellationToken); // Simulate processing time

        var result = new ProcessingResult
        {
            OCRResult = new OCRResult
            {
                Text = "Sample text",
                ConfidenceAvg = 0.95f
            },
            ExtractedFields = new ExtractedFields
            {
                Fechas = new List<string> { "2024-01-01" },
                Montos = new List<AmountData> { new AmountData("MXN", 1000.0m, "1000.00Pesos") }
            }
        };

        // Act
        await _service.CompleteProcessingAsync(context, result, isSuccess: true);

        // Assert
        _service.ActiveProcessingCount.ShouldBe(0);
        var metrics = _service.GetDocumentMetrics(documentId);
        metrics.ShouldNotBeNull();
        metrics!.DocumentId.ShouldBe(documentId);
        metrics.IsSuccess.ShouldBeTrue();
        metrics.Confidence.ShouldBe(0.95f);
        metrics.ExtractedFieldCount.ShouldBe(2); // 1 fecha + 1 monto
    }

    /// <summary>
    /// Tests that RecordErrorAsync records error metrics and decrements active count.
    /// </summary>
    [Fact]
    public async Task RecordErrorAsync_RecordsErrorMetricsAndDecrementsActiveCount()
    {
        // Arrange
        var documentId = "test-doc-3";
        var sourcePath = "test/path/document3.pdf";
        var context = await _service.StartProcessingAsync(documentId, sourcePath);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        var errorMessage = "Processing failed";

        // Act
        await _service.RecordErrorAsync(context, errorMessage);

        // Assert
        _service.ActiveProcessingCount.ShouldBe(0);
        var metrics = _service.GetDocumentMetrics(documentId);
        metrics.ShouldNotBeNull();
        metrics!.IsSuccess.ShouldBeFalse();
        metrics.Confidence.ShouldBe(0.0f);
        metrics.ExtractedFieldCount.ShouldBe(0);

        var events = _service.GetRecentEvents(10);
        var errorEvent = events.FirstOrDefault(e => e.DocumentId == documentId && !e.IsSuccess);
        errorEvent.ShouldNotBeNull();
        errorEvent!.ErrorMessage.ShouldBe(errorMessage);
    }

    /// <summary>
    /// Tests that GetCurrentStatisticsAsync returns current statistics.
    /// </summary>
    [Fact]
    public async Task GetCurrentStatisticsAsync_ReturnsCurrentStatistics()
    {
        // Arrange
        var documentId = "test-doc-4";
        var context = await _service.StartProcessingAsync(documentId, "test/path/document4.pdf");
        await _service.CompleteProcessingAsync(context, null, isSuccess: true);

        // Act
        var statistics = await _service.GetCurrentStatisticsAsync();

        // Assert
        statistics.ShouldNotBeNull();
        // Verify that statistics are updated (LastUpdated should be recent)
        statistics.LastUpdated.ShouldBeGreaterThan(DateTime.UtcNow.AddSeconds(-5));
        // Verify that the document metrics exist (updated immediately)
        var metrics = _service.GetDocumentMetrics(documentId);
        metrics.ShouldNotBeNull();
        metrics!.DocumentId.ShouldBe(documentId);
    }

    /// <summary>
    /// Tests that GetDocumentMetrics returns null for non-existent document.
    /// </summary>
    [Fact]
    public void GetDocumentMetrics_NonExistentDocument_ReturnsNull()
    {
        // Act
        var metrics = _service.GetDocumentMetrics("non-existent");

        // Assert
        metrics.ShouldBeNull();
    }

    /// <summary>
    /// Tests that GetAllMetrics returns all metrics.
    /// </summary>
    [Fact]
    public async Task GetAllMetrics_ReturnsAllMetrics()
    {
        // Arrange
        var context1 = await _service.StartProcessingAsync("doc-1", "path1.pdf");
        var context2 = await _service.StartProcessingAsync("doc-2", "path2.pdf");
        await _service.CompleteProcessingAsync(context1, null, isSuccess: true);
        await _service.CompleteProcessingAsync(context2, null, isSuccess: false);

        // Act
        var allMetrics = _service.GetAllMetrics();

        // Assert
        allMetrics.ShouldNotBeNull();
        allMetrics.Count.ShouldBeGreaterThanOrEqualTo(2);
        allMetrics.Any(m => m.DocumentId == "doc-1").ShouldBeTrue();
        allMetrics.Any(m => m.DocumentId == "doc-2").ShouldBeTrue();
    }

    /// <summary>
    /// Tests that GetRecentEvents returns recent events.
    /// </summary>
    [Fact]
    public async Task GetRecentEvents_ReturnsRecentEvents()
    {
        // Arrange
        var context = await _service.StartProcessingAsync("doc-5", "path5.pdf");
        await _service.CompleteProcessingAsync(context, null, isSuccess: true);

        // Act
        var events = _service.GetRecentEvents(10);

        // Assert
        events.ShouldNotBeNull();
        events.Count.ShouldBeGreaterThanOrEqualTo(1);
        events.Any(e => e.DocumentId == "doc-5").ShouldBeTrue();
    }

    /// <summary>
    /// Tests that CalculateThroughput calculates throughput statistics correctly.
    /// </summary>
    [Fact]
    public void CalculateThroughput_CalculatesThroughputStatistics()
    {
        // Arrange
        var timeSpan = TimeSpan.FromHours(1);

        // Act
        var throughput = _service.CalculateThroughput(timeSpan);

        // Assert
        throughput.ShouldNotBeNull();
        throughput.Period.ShouldBe(timeSpan);
        throughput.TotalDocuments.ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// Tests that ValidatePerformanceAsync validates performance requirements.
    /// </summary>
    [Fact]
    public async Task ValidatePerformanceAsync_ValidatesPerformanceRequirements()
    {
        // Act
        var validation = await _service.ValidatePerformanceAsync();

        // Assert
        validation.IsSuccess.ShouldBeTrue();
        validation.Value.ShouldNotBeNull();
        validation.Value!.CurrentStatistics.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that MaxConcurrency property returns correct value.
    /// </summary>
    [Fact]
    public void MaxConcurrency_ReturnsConfiguredValue()
    {
        // Arrange
        var service = new ProcessingMetricsService(_logger, maxConcurrency: 10);

        // Assert
        service.MaxConcurrency.ShouldBe(10);
    }

    /// <summary>
    /// Tests that ActiveProcessingCount tracks concurrent operations correctly.
    /// </summary>
    [Fact]
    public async Task ActiveProcessingCount_TracksConcurrentOperations()
    {
        // Arrange & Act
        var context1 = await _service.StartProcessingAsync("doc-1", "path1.pdf");
        _service.ActiveProcessingCount.ShouldBe(1);

        var context2 = await _service.StartProcessingAsync("doc-2", "path2.pdf");
        _service.ActiveProcessingCount.ShouldBe(2);

        await _service.CompleteProcessingAsync(context1, null, isSuccess: true);
        _service.ActiveProcessingCount.ShouldBe(1);

        await _service.CompleteProcessingAsync(context2, null, isSuccess: true);
        _service.ActiveProcessingCount.ShouldBe(0);
    }

    /// <summary>
    /// Tests that Dispose releases resources.
    /// </summary>
    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        var service = new ProcessingMetricsService(_logger, maxConcurrency: 5);

        // Act & Assert
        service.Dispose(); // Should not throw
    }
}