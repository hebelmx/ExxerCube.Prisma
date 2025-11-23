namespace ExxerCube.Prisma.Tests.Infrastructure.Metrics;

/// <summary>
/// Unit tests for <see cref="ProcessingContext"/>.
/// </summary>
public class ProcessingContextTests
{
    private readonly ILogger<ProcessingMetricsService> _logger;
    private readonly ProcessingMetricsService _metricsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingContextTests"/> class.
    /// </summary>
    public ProcessingContextTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<ProcessingMetricsService>(output);
        _metricsService = new ProcessingMetricsService(_logger, maxConcurrency: 5);
    }

    /// <summary>
    /// Tests that ProcessingContext properties are set correctly.
    /// </summary>
    [Fact]
    public async Task ProcessingContext_PropertiesSetCorrectly()
    {
        // Arrange
        var documentId = "test-doc-1";
        var sourcePath = "test/path/document.pdf";

        // Act
        var context = await _metricsService.StartProcessingAsync(documentId, sourcePath);

        // Assert
        context.DocumentId.ShouldBe(documentId);
        context.SourcePath.ShouldBe(sourcePath);
        context.Stopwatch.ShouldNotBeNull();
        context.Stopwatch.IsRunning.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that Dispose stops the stopwatch.
    /// </summary>
    [Fact]
    public async Task Dispose_StopsStopwatch()
    {
        // Arrange
        var context = await _metricsService.StartProcessingAsync("test-doc-2", "test/path/document2.pdf");
        var stopwatch = context.Stopwatch;

        // Act
        context.Dispose();

        // Assert
        stopwatch.IsRunning.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that disposing context without completion records an error.
    /// </summary>
    [Fact]
    public async Task Dispose_WithoutCompletion_RecordsError()
    {
        // Arrange
        var documentId = "test-doc-3";
        var context = await _metricsService.StartProcessingAsync(documentId, "test/path/document3.pdf");
        await Task.Delay(50, TestContext.Current.CancellationToken); // Simulate some processing time

        // Act
        context.Dispose();
        await Task.Delay(100, TestContext.Current.CancellationToken); // Allow async error recording to complete

        // Assert
        var metrics = _metricsService.GetDocumentMetrics(documentId);
        metrics.ShouldNotBeNull();
        metrics!.IsSuccess.ShouldBeFalse();

        var events = _metricsService.GetRecentEvents(10);
        var errorEvent = events.FirstOrDefault(e => e.DocumentId == documentId && !e.IsSuccess);
        errorEvent.ShouldNotBeNull();
        errorEvent.ErrorMessage.ShouldNotBeNull();
        errorEvent.ErrorMessage.ShouldContain("disposed without completion");
    }

    /// <summary>
    /// Tests that disposing context after completion does not record error.
    /// </summary>
    [Fact]
    public async Task Dispose_AfterCompletion_DoesNotRecordError()
    {
        // Arrange
        var documentId = "test-doc-4";
        var context = await _metricsService.StartProcessingAsync(documentId, "test/path/document4.pdf");
        await _metricsService.CompleteProcessingAsync(context, null, isSuccess: true);

        // Act
        context.Dispose();
        await Task.Delay(100, TestContext.Current.CancellationToken); // Allow any async operations to complete

        // Assert
        var metrics = _metricsService.GetDocumentMetrics(documentId);
        metrics.ShouldNotBeNull();
        metrics!.IsSuccess.ShouldBeTrue(); // Should remain successful
    }

    /// <summary>
    /// Tests that ProcessingContext implements IProcessingContext.
    /// </summary>
    [Fact]
    public async Task ProcessingContext_ImplementsIProcessingContext()
    {
        // Arrange & Act
        var context = await _metricsService.StartProcessingAsync("test-doc-5", "test/path/document5.pdf");

        // Assert
        context.ShouldBeAssignableTo<IProcessingContext>();
    }

    /// <summary>
    /// Tests that ProcessingContext implements IDisposable.
    /// </summary>
    [Fact]
    public async Task ProcessingContext_ImplementsIDisposable()
    {
        // Arrange & Act
        var context = await _metricsService.StartProcessingAsync("test-doc-6", "test/path/document6.pdf");

        // Assert
        context.ShouldBeAssignableTo<IDisposable>();
    }
}