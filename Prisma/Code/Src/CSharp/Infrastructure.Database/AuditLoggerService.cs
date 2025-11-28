using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Infrastructure.Database;

/// <summary>
/// Service for logging audit records to the database with async, non-blocking operations.
/// </summary>
public class AuditLoggerService : IAuditLogger
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<AuditLoggerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLoggerService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public AuditLoggerService(
        PrismaDbContext dbContext,
        ILogger<AuditLoggerService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
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

            await _dbContext.AuditRecords.AddAsync(auditRecord, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Audit record logged: {AuditId}, action: {ActionType}, stage: {Stage}, correlation: {CorrelationId}",
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
            _logger.LogError(ex, "Failed to log audit record: action={ActionType}, stage={Stage}, correlation={CorrelationId}",
                actionType, stage, correlationId);
            return Result.WithFailure($"Failed to log audit record: {ex.Message}", ex);
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

        try
        {
            var records = await _dbContext.AuditRecords
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

        try
        {
            var records = await _dbContext.AuditRecords
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

        try
        {
            var query = _dbContext.AuditRecords
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
