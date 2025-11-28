using System.Runtime.CompilerServices;
using ExxerCube.Prisma.Domain.Enum;

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

/// <summary>
/// Service for logging audit records using a fire-and-forget queued pattern for true non-blocking operations.
/// Query operations use direct database access for immediate results.
/// </summary>
public class QueuedAuditLoggerService : IAuditLogger
{
    private readonly Channel<AuditRecord> _auditChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QueuedAuditLoggerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedAuditLoggerService"/> class.
    /// </summary>
    /// <param name="processorService">The queued audit processor service that provides the channel.</param>
    /// <param name="scopeFactory">The service scope factory for creating scoped database contexts for queries.</param>
    /// <param name="logger">The logger instance.</param>
    public QueuedAuditLoggerService(
        QueuedAuditProcessorService processorService,
        IServiceScopeFactory scopeFactory,
        ILogger<QueuedAuditLoggerService> logger)
    {
        _auditChannel = processorService?.AuditChannel ?? throw new ArgumentNullException(nameof(processorService));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result> LogAuditAsync(
        AuditActionType actionType,
        ProcessingStage stage,
        string? fileId,
        string correlationId,
        string? userId,
        string? actionDetails,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Audit logging cancelled before starting");
            return ResultExtensions.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            _logger.LogWarning("LogAuditAsync called with null or empty correlationId");
            return Result.WithFailure("CorrelationId cannot be null or empty");
        }

        try
        {
            var auditRecord = new AuditRecord
            {
                AuditId = Guid.NewGuid().ToString(),
                CorrelationId = correlationId,
                FileId = fileId,
                ActionType = actionType,
                ActionDetails = actionDetails,
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Stage = stage,
                Success = success,
                ErrorMessage = errorMessage
            };

            // Fire-and-forget: Queue the record and return immediately
            // This is truly non-blocking - no database write happens here
            await _auditChannel.Writer.WriteAsync(auditRecord, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Audit record queued: {AuditId}, action: {ActionType}, stage: {Stage}, correlation: {CorrelationId}",
                auditRecord.AuditId, actionType, stage, correlationId);

            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Audit logging cancelled");
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue audit record: action={ActionType}, stage={Stage}, correlation={CorrelationId}",
                actionType, stage, correlationId);
            return Result.WithFailure($"Failed to queue audit record: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<AuditRecord>>> GetAuditRecordsByFileIdAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetAuditRecordsByFileIdAsync cancelled before starting");
            return ResultExtensions.Cancelled<List<AuditRecord>>();
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            _logger.LogWarning("GetAuditRecordsByFileIdAsync called with null or empty fileId");
            return Result<List<AuditRecord>>.WithFailure("FileId cannot be null or empty");
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PrismaDbContext>();

        try
        {
            var records = await dbContext.AuditRecords
                .Where(r => r.FileId == fileId)
                .OrderByDescending(r => r.Timestamp)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Retrieved {Count} audit records for file: {FileId}", records.Count, fileId);
            return Result<List<AuditRecord>>.Success(records);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetAuditRecordsByFileIdAsync cancelled");
            return ResultExtensions.Cancelled<List<AuditRecord>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit records for file: {FileId}", fileId);
            return Result<List<AuditRecord>>.WithFailure($"Error retrieving audit records: {ex.Message}", default, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<AuditRecord>>> GetAuditRecordsByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetAuditRecordsByCorrelationIdAsync cancelled before starting");
            return ResultExtensions.Cancelled<List<AuditRecord>>();
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            _logger.LogWarning("GetAuditRecordsByCorrelationIdAsync called with null or empty correlationId");
            return Result<List<AuditRecord>>.WithFailure("CorrelationId cannot be null or empty");
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PrismaDbContext>();

        try
        {
            var records = await dbContext.AuditRecords
                .Where(r => r.CorrelationId == correlationId)
                .OrderBy(r => r.Timestamp)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Retrieved {Count} audit records for correlation: {CorrelationId}", records.Count, correlationId);
            return Result<List<AuditRecord>>.Success(records);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetAuditRecordsByCorrelationIdAsync cancelled");
            return ResultExtensions.Cancelled<List<AuditRecord>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit records for correlation: {CorrelationId}", correlationId);
            return Result<List<AuditRecord>>.WithFailure($"Error retrieving audit records: {ex.Message}", default, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<AuditRecord>>> GetAuditRecordsAsync(
        DateTime startDate,
        DateTime endDate,
        AuditActionType? actionType,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetAuditRecordsAsync cancelled before starting");
            return ResultExtensions.Cancelled<List<AuditRecord>>();
        }

        if (endDate < startDate)
        {
            _logger.LogWarning("GetAuditRecordsAsync called with invalid date range: start={StartDate}, end={EndDate}",
                startDate, endDate);
            return Result<List<AuditRecord>>.WithFailure("EndDate must be greater than or equal to StartDate");
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PrismaDbContext>();

        try
        {
            var query = dbContext.AuditRecords
                .Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate);

            if (actionType is not null)
            {
                query = query.Where(r => r.ActionType == actionType);
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(r => r.UserId == userId);
            }

            var records = await query
                .OrderByDescending(r => r.Timestamp)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Retrieved {Count} audit records for date range: {StartDate} to {EndDate}",
                records.Count, startDate, endDate);
            return Result<List<AuditRecord>>.Success(records);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetAuditRecordsAsync cancelled");
            return ResultExtensions.Cancelled<List<AuditRecord>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit records for date range: {StartDate} to {EndDate}",
                startDate, endDate);
            return Result<List<AuditRecord>>.WithFailure($"Error retrieving audit records: {ex.Message}", default, ex);
        }
    }
}
