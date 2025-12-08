using System.Runtime.CompilerServices;

namespace ExxerCube.Prisma.Infrastructure.Database.Services;

/// <summary>
/// Background service that processes queued audit records in batches for efficient database writes.
/// </summary>
public class QueuedAuditProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QueuedAuditProcessorService> _logger;
    private readonly AuditOptions _auditOptions;
    private readonly Channel<AuditRecord> _auditChannel;
    private const int BatchSize = 100;
    private const int BatchTimeoutMs = 1000; // 1 second

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedAuditProcessorService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="auditOptions">The audit configuration options.</param>
    public QueuedAuditProcessorService(
        IServiceScopeFactory scopeFactory,
        ILogger<QueuedAuditProcessorService> logger,
        IOptions<AuditOptions> auditOptions)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditOptions = auditOptions?.Value ?? throw new ArgumentNullException(nameof(auditOptions));

        // Create bounded channel with capacity to handle bursts
        var options = new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };
        _auditChannel = Channel.CreateBounded<AuditRecord>(options);
    }

    /// <summary>
    /// Gets the audit channel for queuing audit records.
    /// </summary>
    public Channel<AuditRecord> AuditChannel => _auditChannel;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queued Audit Processor Service started. Batch size: {BatchSize}, Batch timeout: {BatchTimeoutMs}ms", BatchSize, BatchTimeoutMs);

        await foreach (var batch in GetBatchesAsync(stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (batch.Count > 0)
            {
                await ProcessBatchAsync(batch, stoppingToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Queued Audit Processor Service stopped");
    }

    /// <summary>
    /// Reads audit records from the channel and groups them into batches.
    /// </summary>
    private async IAsyncEnumerable<List<AuditRecord>> GetBatchesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var batch = new List<AuditRecord>(BatchSize);
        var batchTimer = System.Diagnostics.Stopwatch.StartNew();

        await using var enumerator = _auditChannel.Reader
            .ReadAllAsync(cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            bool moveNextSucceeded;

            try
            {
                moveNextSucceeded = await enumerator.MoveNextAsync();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Batch reading cancelled, processing remaining records");
                break;
            }

            if (!moveNextSucceeded)
                break;

            var record = enumerator.Current;
            batch.Add(record);

            if (batch.Count >= BatchSize || batchTimer.ElapsedMilliseconds >= BatchTimeoutMs)
            {
                yield return batch;
                batch = new List<AuditRecord>(BatchSize);
                batchTimer.Restart();
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    /// <summary>
    /// Processes a batch of audit records by writing them to the database.
    /// </summary>
    private async Task ProcessBatchAsync(List<AuditRecord> batch, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PrismaDbContext>();

        try
        {
            await dbContext.AuditRecords.AddRangeAsync(batch, cancellationToken).ConfigureAwait(false);
            var savedCount = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Successfully saved batch of {Count} audit records", savedCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Batch processing cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch of {Count} audit records", batch.Count);
            // Note: In production, you might want to retry failed batches or log to dead-letter queue
        }
    }
}