namespace ExxerCube.Prisma.Infrastructure.Database.Services;

/// <summary>
/// Background service that periodically enforces audit log retention policies.
/// Archives records older than ArchiveAfterYears and deletes records older than RetentionYears.
/// </summary>
public class AuditRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditRetentionBackgroundService> _logger;
    private readonly AuditOptions _auditOptions;
    private readonly AuditRetentionOptions _retentionOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditRetentionBackgroundService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="auditOptions">The audit configuration options.</param>
    /// <param name="retentionOptions">The retention background service configuration options.</param>
    public AuditRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditRetentionBackgroundService> logger,
        IOptions<AuditOptions> auditOptions,
        IOptions<AuditRetentionOptions> retentionOptions)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditOptions = auditOptions?.Value ?? throw new ArgumentNullException(nameof(auditOptions));
        _retentionOptions = retentionOptions?.Value ?? throw new ArgumentNullException(nameof(retentionOptions));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Audit Retention Background Service started. Retention: {RetentionYears} years, Archive after: {ArchiveAfterYears} years, Interval: {IntervalHours}h, Batch size: {BatchSize}",
            _auditOptions.RetentionYears, _auditOptions.ArchiveAfterYears, _retentionOptions.IntervalHours, _retentionOptions.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnforceRetentionPoliciesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Audit Retention Background Service cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Audit Retention Background Service. Will retry after delay.");

                // Wait before retrying to avoid tight error loops
                try
                {
                    await Task.Delay(TimeSpan.FromHours(_retentionOptions.RetryDelayHours), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            // Wait for the configured interval before next retention cycle
            if (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(_retentionOptions.IntervalHours), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Audit Retention Background Service stopped");
    }

    /// <summary>
    /// Enforces audit log retention policies by archiving and deleting old records.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    private async Task EnforceRetentionPoliciesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PrismaDbContext>();

        var archiveCutoffDate = DateTime.UtcNow.AddYears(-_auditOptions.ArchiveAfterYears);
        var deletionCutoffDate = DateTime.UtcNow.AddYears(-_auditOptions.RetentionYears);

        _logger.LogInformation(
            "Starting retention policy enforcement. Archive cutoff: {ArchiveCutoff}, Deletion cutoff: {DeletionCutoff}",
            archiveCutoffDate, deletionCutoffDate);

        // Step 1: Archive records older than ArchiveAfterYears (if archiving is enabled)
        if (!string.IsNullOrWhiteSpace(_auditOptions.ArchiveLocation))
        {
            await ArchiveOldRecordsAsync(dbContext, archiveCutoffDate, cancellationToken).ConfigureAwait(false);
        }

        // Step 2: Delete records older than RetentionYears (if auto-deletion is enabled)
        if (_auditOptions.AutoDeleteAfterRetention)
        {
            await DeleteOldRecordsAsync(dbContext, deletionCutoffDate, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug("Auto-deletion is disabled. Records older than {DeletionCutoff} will not be deleted.", deletionCutoffDate);
        }
    }

    /// <summary>
    /// Archives audit records older than the specified cutoff date to cold storage.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="cutoffDate">The cutoff date for archiving.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    private async Task ArchiveOldRecordsAsync(PrismaDbContext dbContext, DateTime cutoffDate, CancellationToken cancellationToken)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Archive operation cancelled before starting");
            return;
        }

        try
        {
            // Get records to archive (older than cutoff date, but not older than deletion cutoff)
            var recordsToArchive = await dbContext.AuditRecords
                .Where(r => r.Timestamp < cutoffDate)
                .OrderBy(r => r.Timestamp)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (!recordsToArchive.Any())
            {
                _logger.LogDebug("No records to archive (cutoff: {CutoffDate})", cutoffDate);
                return;
            }

            _logger.LogInformation("Archiving {Count} audit records older than {CutoffDate}", recordsToArchive.Count, cutoffDate);

            // Ensure archive directory exists
            var archiveDir = Path.Combine(_auditOptions.ArchiveLocation!, DateTime.UtcNow.ToString("yyyy-MM"));
            Directory.CreateDirectory(archiveDir);

            var archiveFileName = $"audit_records_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var archiveFilePath = Path.Combine(archiveDir, archiveFileName);

            // Serialize records to JSON and write to archive file
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var jsonContent = JsonSerializer.Serialize(recordsToArchive, jsonOptions);
            await File.WriteAllTextAsync(archiveFilePath, jsonContent, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully archived {Count} audit records to {ArchivePath}", recordsToArchive.Count, archiveFilePath);

            // Note: Records are not deleted after archiving - they remain in database until deletion cutoff
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Archive operation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving audit records");
            throw;
        }
    }

    /// <summary>
    /// Deletes audit records older than the specified cutoff date.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="cutoffDate">The cutoff date for deletion.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    private async Task DeleteOldRecordsAsync(PrismaDbContext dbContext, DateTime cutoffDate, CancellationToken cancellationToken)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Deletion operation cancelled before starting");
            return;
        }

        try
        {
            // Get count of records to delete (for logging)
            var recordsToDeleteCount = await dbContext.AuditRecords
                .Where(r => r.Timestamp < cutoffDate)
                .CountAsync(cancellationToken).ConfigureAwait(false);

            if (recordsToDeleteCount == 0)
            {
                _logger.LogDebug("No records to delete (cutoff: {CutoffDate})", cutoffDate);
                return;
            }

            _logger.LogInformation("Deleting {Count} audit records older than {CutoffDate}", recordsToDeleteCount, cutoffDate);

            // Delete in batches to avoid memory issues and long-running transactions
            var totalDeleted = 0;
            var batchSize = _retentionOptions.BatchSize;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Deletion operation cancelled during batch processing");
                    break;
                }

                // Get batch of records to delete
                var batch = await dbContext.AuditRecords
                    .Where(r => r.Timestamp < cutoffDate)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                if (!batch.Any())
                {
                    break; // No more records to delete
                }

                // Delete batch
                dbContext.AuditRecords.RemoveRange(batch);
                var deletedInBatch = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                totalDeleted += deletedInBatch;

                _logger.LogDebug("Deleted batch of {Count} audit records. Total deleted: {Total}", deletedInBatch, totalDeleted);

                // Small delay between batches to avoid overwhelming the database
                if (totalDeleted < recordsToDeleteCount)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                }
            }

            _logger.LogInformation("Successfully deleted {Count} audit records older than {CutoffDate}", totalDeleted, cutoffDate);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Deletion operation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting audit records");
            throw;
        }
    }
}

