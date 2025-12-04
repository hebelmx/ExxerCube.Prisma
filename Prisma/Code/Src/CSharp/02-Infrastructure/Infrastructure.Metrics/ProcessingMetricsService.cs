using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Metrics;

/// <summary>
/// Service for collecting and managing performance metrics for OCR processing.
/// Implements metrics collection, performance monitoring, and throughput analysis.
/// </summary>
public class ProcessingMetricsService : IDisposable, IProcessingMetricsService
{
    private readonly ILogger<ProcessingMetricsService> _logger;
    private readonly ConcurrentDictionary<string, ProcessingMetrics> _documentMetrics;
    private readonly ConcurrentQueue<ProcessingEvent> _processingEvents;
    private readonly Timer _metricsAggregationTimer;
    private readonly SemaphoreSlim _metricsLock;
    private bool _disposed;

    /// <summary>
    /// Gets the current processing statistics.
    /// </summary>
    public ProcessingStatistics CurrentStatistics { get; private set; }

    /// <summary>
    /// Gets the maximum number of concurrent processing operations.
    /// </summary>
    public int MaxConcurrency { get; }

    /// <summary>
    /// Gets the current number of active processing operations.
    /// </summary>
    public int ActiveProcessingCount { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingMetricsService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="maxConcurrency">The maximum number of concurrent processing operations.</param>
    public ProcessingMetricsService(ILogger<ProcessingMetricsService> logger, int maxConcurrency = 5)
    {
        _logger = logger;
        MaxConcurrency = maxConcurrency;
        _documentMetrics = new ConcurrentDictionary<string, ProcessingMetrics>();
        _processingEvents = new ConcurrentQueue<ProcessingEvent>();
        _metricsLock = new SemaphoreSlim(1, 1);
        CurrentStatistics = new ProcessingStatistics();

        // Start metrics aggregation timer (every 30 seconds)
        _metricsAggregationTimer = new Timer(AggregateMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

        _logger.LogInformation("Processing metrics service initialized with max concurrency: {MaxConcurrency}", maxConcurrency);
    }

    /// <summary>
    /// Records the start of a document processing operation.
    /// </summary>
    /// <param name="documentId">The unique identifier for the document.</param>
    /// <param name="sourcePath">The source path of the document.</param>
    /// <returns>A processing context that should be disposed when processing completes.</returns>
    public async Task<IProcessingContext> StartProcessingAsync(string documentId, string sourcePath)
    {
        await _metricsLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (ActiveProcessingCount >= MaxConcurrency)
            {
                _logger.LogWarning("Maximum concurrency reached ({MaxConcurrency}), processing may be queued", MaxConcurrency);
            }

            ActiveProcessingCount++;
            var stopwatch = Stopwatch.StartNew();
            var context = new ProcessingContext(documentId, sourcePath, stopwatch, this);

            _logger.LogDebug("Started processing document {DocumentId} from {SourcePath}. Active: {ActiveCount}/{MaxConcurrency}",
                documentId, sourcePath, ActiveProcessingCount, MaxConcurrency);

            return context;
        }
        finally
        {
            _metricsLock.Release();
        }
    }

    /// <summary>
    /// Records the completion of a document processing operation.
    /// </summary>
    /// <param name="context">The processing context.</param>
    /// <param name="result">The processing result.</param>
    /// <param name="isSuccess">Whether the processing was successful.</param>
    public async Task CompleteProcessingAsync(IProcessingContext context, ProcessingResult? result, bool isSuccess)
    {
        await _metricsLock.WaitAsync().ConfigureAwait(false);
        try
        {
            ActiveProcessingCount--;
            context.Stopwatch.Stop();

            var processingTime = context.Stopwatch.Elapsed.TotalSeconds;
            var confidence = result?.OCRResult.ConfidenceAvg ?? 0.0f;
            var fieldCount = (result?.ExtractedFields.Fechas.Count ?? 0) + (result?.ExtractedFields.Montos.Count ?? 0);

            var metrics = new ProcessingMetrics
            {
                DocumentId = context.DocumentId,
                SourcePath = context.SourcePath,
                ProcessingTimeSeconds = processingTime,
                Confidence = confidence,
                ExtractedFieldCount = fieldCount,
                IsSuccess = isSuccess,
                CompletedAt = DateTime.UtcNow
            };

            _documentMetrics.TryAdd(context.DocumentId, metrics);

            var processingEvent = new ProcessingEvent
            {
                DocumentId = context.DocumentId,
                ProcessingTimeSeconds = processingTime,
                IsSuccess = isSuccess,
                Confidence = confidence,
                Timestamp = DateTime.UtcNow
            };

            _processingEvents.Enqueue(processingEvent);

            _logger.LogInformation("Completed processing document {DocumentId} in {ProcessingTime:F2}s. Success: {IsSuccess}, Confidence: {Confidence:F2}%, Fields: {FieldCount}. Active: {ActiveCount}/{MaxConcurrency}",
                context.DocumentId, processingTime, isSuccess, confidence * 100, fieldCount, ActiveProcessingCount, MaxConcurrency);

            // Update current statistics
            UpdateCurrentStatistics();
        }
        finally
        {
            _metricsLock.Release();
        }
    }

    /// <summary>
    /// Records a processing error.
    /// </summary>
    /// <param name="context">The processing context.</param>
    /// <param name="error">The error message.</param>
    public async Task RecordErrorAsync(IProcessingContext context, string error)
    {
        await _metricsLock.WaitAsync().ConfigureAwait(false);
        try
        {
            ActiveProcessingCount--;
            context.Stopwatch.Stop();

            var processingTime = context.Stopwatch.Elapsed.TotalSeconds;

            var metrics = new ProcessingMetrics
            {
                DocumentId = context.DocumentId,
                SourcePath = context.SourcePath,
                ProcessingTimeSeconds = processingTime,
                Confidence = 0.0f,
                ExtractedFieldCount = 0,
                IsSuccess = false,
                CompletedAt = DateTime.UtcNow
            };

            _documentMetrics.TryAdd(context.DocumentId, metrics);

            var errorEvent = new ProcessingEvent
            {
                DocumentId = context.DocumentId,
                ProcessingTimeSeconds = processingTime,
                IsSuccess = false,
                Confidence = 0.0f,
                ErrorMessage = error,
                Timestamp = DateTime.UtcNow
            };

            _processingEvents.Enqueue(errorEvent);

            _logger.LogError("Processing error for document {DocumentId} in {ProcessingTime:F2}s: {Error}. Active: {ActiveCount}/{MaxConcurrency}",
                context.DocumentId, processingTime, error, ActiveProcessingCount, MaxConcurrency);

            // Update current statistics
            UpdateCurrentStatistics();
        }
        finally
        {
            _metricsLock.Release();
        }
    }

    /// <summary>
    /// Gets the current processing statistics.
    /// </summary>
    /// <returns>The current processing statistics.</returns>
    public async Task<ProcessingStatistics> GetCurrentStatisticsAsync()
    {
        await _metricsLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return CurrentStatistics;
        }
        finally
        {
            _metricsLock.Release();
        }
    }

    /// <summary>
    /// Gets detailed metrics for a specific document.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <returns>The processing metrics for the document, or null if not found.</returns>
    public ProcessingMetrics? GetDocumentMetrics(string documentId)
    {
        return _documentMetrics.TryGetValue(documentId, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// Gets all processing metrics.
    /// </summary>
    /// <returns>A list of all processing metrics.</returns>
    public List<ProcessingMetrics> GetAllMetrics()
    {
        return _documentMetrics.Values.ToList();
    }

    /// <summary>
    /// Gets recent processing events.
    /// </summary>
    /// <param name="count">The number of recent events to retrieve.</param>
    /// <returns>A list of recent processing events.</returns>
    public List<ProcessingEvent> GetRecentEvents(int count = 100)
    {
        return _processingEvents.TakeLast(count).ToList();
    }

    /// <summary>
    /// Calculates throughput metrics for the specified time period.
    /// </summary>
    /// <param name="timeSpan">The time period to analyze.</param>
    /// <returns>Throughput statistics for the period.</returns>
    public ThroughputStatistics CalculateThroughput(TimeSpan timeSpan)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(timeSpan);
        var recentEvents = _processingEvents.Where(e => e.Timestamp >= cutoffTime).ToList();

        if (!recentEvents.Any())
        {
            return new ThroughputStatistics
            {
                Period = timeSpan,
                TotalDocuments = 0,
                SuccessfulDocuments = 0,
                FailedDocuments = 0,
                AverageProcessingTime = 0,
                DocumentsPerHour = 0,
                SuccessRate = 0
            };
        }

        var totalDocuments = recentEvents.Count;
        var successfulDocuments = recentEvents.Count(e => e.IsSuccess);
        var failedDocuments = totalDocuments - successfulDocuments;
        var averageProcessingTime = recentEvents.Average(e => e.ProcessingTimeSeconds);
        var documentsPerHour = totalDocuments / timeSpan.TotalHours;
        var successRate = (double)successfulDocuments / totalDocuments;

        return new ThroughputStatistics
        {
            Period = timeSpan,
            TotalDocuments = totalDocuments,
            SuccessfulDocuments = successfulDocuments,
            FailedDocuments = failedDocuments,
            AverageProcessingTime = averageProcessingTime,
            DocumentsPerHour = documentsPerHour,
            SuccessRate = successRate
        };
    }

    /// <summary>
    /// Checks if the system is meeting performance requirements.
    /// </summary>
    /// <returns>A result indicating whether performance requirements are met.</returns>
    public async Task<Result<PerformanceValidation>> ValidatePerformanceAsync()
    {
        var statistics = await GetCurrentStatisticsAsync().ConfigureAwait(false);
        var throughput1Hour = CalculateThroughput(TimeSpan.FromHours(1));
        var throughput5Minutes = CalculateThroughput(TimeSpan.FromMinutes(5));

        var validation = new PerformanceValidation
        {
            IsMeetingRequirements = true,
            ValidationResults = new List<string>()
        };

        // Check throughput requirements (100+ documents per hour)
        if (throughput1Hour.DocumentsPerHour < 100)
        {
            validation.IsMeetingRequirements = false;
            validation.ValidationResults.Add($"Throughput requirement not met: {throughput1Hour.DocumentsPerHour:F1} docs/hour (required: 100+)");
        }

        // Check processing time requirements (<30 seconds per document)
        if (statistics.AverageProcessingTime > 30)
        {
            validation.IsMeetingRequirements = false;
            validation.ValidationResults.Add($"Processing time requirement not met: {statistics.AverageProcessingTime:F1}s (required: <30s)");
        }

        // Check concurrency requirements (5+ concurrent documents)
        if (MaxConcurrency < 5)
        {
            validation.IsMeetingRequirements = false;
            validation.ValidationResults.Add($"Concurrency requirement not met: {MaxConcurrency} (required: 5+)");
        }

        // Check success rate requirements (>99%)
        if (statistics.SuccessRate < 0.99)
        {
            validation.IsMeetingRequirements = false;
            validation.ValidationResults.Add($"Success rate requirement not met: {statistics.SuccessRate:P1} (required: >99%)");
        }

        validation.Throughput1Hour = throughput1Hour;
        validation.Throughput5Minutes = throughput5Minutes;
        validation.CurrentStatistics = statistics;

        return Result<PerformanceValidation>.Success(validation);
    }

    /// <summary>
    /// Aggregates metrics and updates current statistics.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    private void AggregateMetrics(object? state)
    {
        try
        {
            var allMetrics = GetAllMetrics();
            if (!allMetrics.Any()) return;

            var successfulMetrics = allMetrics.Where(m => m.IsSuccess).ToList();
            var failedMetrics = allMetrics.Where(m => !m.IsSuccess).ToList();

            var totalDocuments = allMetrics.Count;
            var successfulDocuments = successfulMetrics.Count;
            var failedDocuments = failedMetrics.Count;

            var averageProcessingTime = allMetrics.Average(m => m.ProcessingTimeSeconds);
            var averageConfidence = successfulMetrics.Any() ? successfulMetrics.Average(m => m.Confidence) : 0.0f;
            var averageFieldCount = allMetrics.Average(m => m.ExtractedFieldCount);
            var successRate = totalDocuments > 0 ? (double)successfulDocuments / totalDocuments : 0.0;

            CurrentStatistics = new ProcessingStatistics
            {
                TotalDocumentsProcessed = totalDocuments,
                SuccessfulDocuments = successfulDocuments,
                FailedDocuments = failedDocuments,
                AverageProcessingTime = averageProcessingTime,
                AverageConfidence = averageConfidence,
                AverageExtractedFields = averageFieldCount,
                SuccessRate = successRate,
                LastUpdated = DateTime.UtcNow
            };

            _logger.LogDebug("Metrics aggregated: {TotalDocs} total, {SuccessDocs} successful, {FailedDocs} failed, Avg time: {AvgTime:F2}s, Success rate: {SuccessRate:P1}",
                totalDocuments, successfulDocuments, failedDocuments, averageProcessingTime, successRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating metrics");
        }
    }

    /// <summary>
    /// Updates the current statistics based on recent events.
    /// </summary>
    private void UpdateCurrentStatistics()
    {
        // This method is called after each processing completion
        // The full aggregation happens in the timer callback
        var recentEvents = GetRecentEvents(100);
        if (!recentEvents.Any()) return;

        var recentSuccessful = recentEvents.Where(e => e.IsSuccess).ToList();
        var recentFailed = recentEvents.Where(e => !e.IsSuccess).ToList();

        var totalRecent = recentEvents.Count;
        var successfulRecent = recentSuccessful.Count;
        var averageTimeRecent = recentEvents.Average(e => e.ProcessingTimeSeconds);
        var successRateRecent = totalRecent > 0 ? (double)successfulRecent / totalRecent : 0.0;

        CurrentStatistics = new ProcessingStatistics
        {
            TotalDocumentsProcessed = CurrentStatistics.TotalDocumentsProcessed,
            SuccessfulDocuments = CurrentStatistics.SuccessfulDocuments,
            FailedDocuments = CurrentStatistics.FailedDocuments,
            AverageProcessingTime = averageTimeRecent,
            AverageConfidence = recentSuccessful.Any() ? recentSuccessful.Average(e => e.Confidence) : 0.0f,
            AverageExtractedFields = CurrentStatistics.AverageExtractedFields,
            SuccessRate = successRateRecent,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Disposes the metrics service and releases resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the metrics service and releases resources.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _metricsAggregationTimer?.Dispose();
            _metricsLock?.Dispose();
            _disposed = true;
        }
    }
}