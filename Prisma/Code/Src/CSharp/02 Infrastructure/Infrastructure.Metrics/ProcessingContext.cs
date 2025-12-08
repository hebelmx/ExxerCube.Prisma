namespace ExxerCube.Prisma.Infrastructure.Metrics;

/// <summary>
/// Represents a processing context for tracking individual document processing.
/// </summary>
public class ProcessingContext : IProcessingContext
{
    private readonly IProcessingMetricsService _metricsService;
    private bool _disposed;

    /// <summary>
    /// Gets the document identifier.
    /// </summary>
    public string DocumentId { get; }

    /// <summary>
    /// Gets the source path of the document.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// Gets the stopwatch for timing the processing.
    /// </summary>
    public Stopwatch Stopwatch { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingContext"/> class.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="sourcePath">The source path.</param>
    /// <param name="stopwatch">The stopwatch.</param>
    /// <param name="metricsService">The metrics service.</param>
    internal ProcessingContext(string documentId, string sourcePath, Stopwatch stopwatch, IProcessingMetricsService metricsService)
    {
        DocumentId = documentId;
        SourcePath = sourcePath;
        Stopwatch = stopwatch;
        _metricsService = metricsService;
    }

    /// <summary>
    /// Disposes the processing context.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the processing context.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            // If the context is disposed without calling CompleteProcessingAsync,
            // we should record it as an error
            if (Stopwatch.IsRunning)
            {
                Stopwatch.Stop();
                _ = _metricsService.RecordErrorAsync(this, "Processing context disposed without completion");
            }
            _disposed = true;
        }
    }
}