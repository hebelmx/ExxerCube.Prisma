using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Infrastructure.Database;

/// <summary>
/// Service for enforcing SLA deadlines and managing escalations.
/// </summary>
public class SLAEnforcerService : ISLAEnforcer
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<SLAEnforcerService> _logger;
    private readonly SLAOptions _options;
    private readonly SLAMetricsCollector _metricsCollector;

    /// <summary>
    /// Initializes a new instance of the <see cref="SLAEnforcerService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The SLA configuration options.</param>
    /// <param name="metricsCollector">The metrics collector for SLA operations.</param>
    public SLAEnforcerService(
        PrismaDbContext dbContext,
        ILogger<SLAEnforcerService> logger,
        IOptions<SLAOptions> options,
        SLAMetricsCollector metricsCollector)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
    }

    /// <inheritdoc />
    public async Task<Result<SLAStatus>> CalculateSLAStatusAsync(
        string fileId,
        DateTime intakeDate,
        int daysPlazo,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("SLA calculation cancelled before starting for file: {FileId}", fileId);
            return ResultExtensions.Cancelled<SLAStatus>();
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            _logger.LogWarning("CalculateSLAStatusAsync called with null or empty fileId");
            return Result<SLAStatus>.WithFailure("FileId cannot be null or empty");
        }

        if (daysPlazo <= 0)
        {
            _logger.LogWarning("CalculateSLAStatusAsync called with invalid daysPlazo: {DaysPlazo}", daysPlazo);
            return Result<SLAStatus>.WithFailure("DaysPlazo must be greater than zero");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Calculating SLA status for file: {FileId}, intake date: {IntakeDate}, days plazo: {DaysPlazo}",
                fileId, intakeDate, daysPlazo);

            // Calculate deadline by adding business days (excluding weekends)
            var deadline = AddBusinessDays(intakeDate, daysPlazo);
            var now = DateTime.UtcNow;
            var remainingTime = deadline > now ? deadline - now : TimeSpan.Zero;
            var isBreached = deadline <= now;
            var isAtRisk = !isBreached && remainingTime <= _options.CriticalThreshold;

            // Determine escalation level
            var escalationLevel = DetermineEscalationLevel(remainingTime, isBreached);

            var slaStatus = new SLAStatus
            {
                FileId = fileId,
                IntakeDate = intakeDate,
                Deadline = deadline,
                DaysPlazo = daysPlazo,
                RemainingTime = remainingTime,
                IsAtRisk = isAtRisk,
                IsBreached = isBreached,
                EscalationLevel = escalationLevel,
                EscalatedAt = escalationLevel != EscalationLevel.None ? now : null
            };

            // Save or update in database
            var existingStatus = await _dbContext.SLAStatus
                .FirstOrDefaultAsync(s => s.FileId == fileId, cancellationToken).ConfigureAwait(false);

            if (existingStatus != null)
            {
                // Update existing
                existingStatus.IntakeDate = slaStatus.IntakeDate;
                existingStatus.Deadline = slaStatus.Deadline;
                existingStatus.DaysPlazo = slaStatus.DaysPlazo;
                existingStatus.RemainingTime = slaStatus.RemainingTime;
                existingStatus.IsAtRisk = slaStatus.IsAtRisk;
                existingStatus.IsBreached = slaStatus.IsBreached;
                existingStatus.EscalationLevel = slaStatus.EscalationLevel;
                existingStatus.EscalatedAt = slaStatus.EscalatedAt;
            }
            else
            {
                // Create new
                await _dbContext.SLAStatus.AddAsync(slaStatus, cancellationToken).ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("SLA status calculated for file: {FileId}, deadline: {Deadline}, escalation: {EscalationLevel}",
                fileId, deadline, escalationLevel);

            return Result<SLAStatus>.Success(slaStatus);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogInformation("SLA calculation cancelled");
            return ResultExtensions.Cancelled<SLAStatus>();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metricsCollector.RecordCalculation(stopwatch.Elapsed.TotalMilliseconds, false);
            _metricsCollector.RecordError("calculation", ex.GetType().Name);
            _logger.LogError(ex, "Error calculating SLA status for file: {FileId}", fileId);
            return Result<SLAStatus>.WithFailure($"Error calculating SLA status: {ex.Message}", default, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<SLAStatus>> UpdateSLAStatusAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("SLA status update cancelled before starting for file: {FileId}", fileId);
            return ResultExtensions.Cancelled<SLAStatus>();
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            return Result<SLAStatus>.WithFailure("FileId cannot be null or empty");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var existingStatus = await _dbContext.SLAStatus
                .FirstOrDefaultAsync(s => s.FileId == fileId, cancellationToken).ConfigureAwait(false);

            if (existingStatus == null)
            {
                stopwatch.Stop();
                _metricsCollector.RecordUpdate(stopwatch.Elapsed.TotalMilliseconds, false);
                _logger.LogWarning("SLA status not found for file: {FileId}", fileId);
                return Result<SLAStatus>.WithFailure($"SLA status not found for file: {fileId}");
            }

            // Recalculate based on current time
            var now = DateTime.UtcNow;
            var remainingTime = existingStatus.Deadline > now ? existingStatus.Deadline - now : TimeSpan.Zero;
            var isBreached = existingStatus.Deadline <= now;
            var isAtRisk = !isBreached && remainingTime <= _options.CriticalThreshold;
            var escalationLevel = DetermineEscalationLevel(remainingTime, isBreached);

            // Update if escalation level changed
            if (existingStatus.EscalationLevel != escalationLevel)
            {
                existingStatus.EscalationLevel = escalationLevel;
                existingStatus.EscalatedAt = escalationLevel != EscalationLevel.None ? now : existingStatus.EscalatedAt;
            }

            existingStatus.RemainingTime = remainingTime;
            existingStatus.IsAtRisk = isAtRisk;
            existingStatus.IsBreached = isBreached;

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            _metricsCollector.RecordUpdate(stopwatch.Elapsed.TotalMilliseconds, true);

            _logger.LogInformation("SLA status updated for file: {FileId}, escalation: {EscalationLevel}",
                fileId, existingStatus.EscalationLevel);

            return Result<SLAStatus>.Success(existingStatus);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogInformation("SLA status update cancelled");
            return ResultExtensions.Cancelled<SLAStatus>();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metricsCollector.RecordUpdate(stopwatch.Elapsed.TotalMilliseconds, false);
            _metricsCollector.RecordError("update", ex.GetType().Name);
            _logger.LogError(ex, "Error updating SLA status for file: {FileId}", fileId);
            return Result<SLAStatus>.WithFailure($"Error updating SLA status: {ex.Message}", default, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<SLAStatus?>> GetSLAStatusAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetSLAStatusAsync cancelled before starting for file: {FileId}", fileId);
            return ResultExtensions.Cancelled<SLAStatus?>();
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            return Result<SLAStatus?>.WithFailure("FileId cannot be null or empty");
        }

        try
        {
            var status = await _dbContext.SLAStatus
                .FirstOrDefaultAsync(s => s.FileId == fileId, cancellationToken).ConfigureAwait(false);

            return Result<SLAStatus?>.Success(status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetSLAStatusAsync cancelled");
            return ResultExtensions.Cancelled<SLAStatus?>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SLA status for file: {FileId}", fileId);
            return Result<SLAStatus?>.WithFailure($"Error retrieving SLA status: {ex.Message}", default, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<SLAStatus>>> GetAtRiskCasesAsync(
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetAtRiskCasesAsync cancelled before starting");
            return ResultExtensions.Cancelled<List<SLAStatus>>();
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var atRiskCases = await _dbContext.SLAStatus
                .Where(s => s.IsAtRisk && !s.IsBreached)
                .OrderBy(s => s.Deadline)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            _metricsCollector.RecordQuery(stopwatch.Elapsed.TotalMilliseconds, "at_risk", true);
            _metricsCollector.UpdateAtRiskCases(atRiskCases.Count);

            _logger.LogInformation("Retrieved {Count} at-risk cases", atRiskCases.Count);
            return Result<List<SLAStatus>>.Success(atRiskCases);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetAtRiskCasesAsync cancelled");
            return ResultExtensions.Cancelled<List<SLAStatus>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving at-risk cases");
            return Result<List<SLAStatus>>.WithFailure($"Error retrieving at-risk cases: {ex.Message}", default, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<SLAStatus>>> GetBreachedCasesAsync(
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetBreachedCasesAsync cancelled before starting");
            return ResultExtensions.Cancelled<List<SLAStatus>>();
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var breachedCases = await _dbContext.SLAStatus
                .Where(s => s.IsBreached)
                .OrderByDescending(s => s.Deadline)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            _metricsCollector.RecordQuery(stopwatch.Elapsed.TotalMilliseconds, "breached", true);
            _metricsCollector.UpdateBreachedCases(breachedCases.Count);

            _logger.LogInformation("Retrieved {Count} breached cases", breachedCases.Count);
            return Result<List<SLAStatus>>.Success(breachedCases);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetBreachedCasesAsync cancelled");
            return ResultExtensions.Cancelled<List<SLAStatus>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving breached cases");
            return Result<List<SLAStatus>>.WithFailure($"Error retrieving breached cases: {ex.Message}", default, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<SLAStatus>>> GetActiveCasesAsync(
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetActiveCasesAsync cancelled before starting");
            return ResultExtensions.Cancelled<List<SLAStatus>>();
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var activeCases = await _dbContext.SLAStatus
                .Where(s => !s.IsBreached)
                .OrderBy(s => s.Deadline)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            _metricsCollector.RecordQuery(stopwatch.Elapsed.TotalMilliseconds, "active", true);
            _metricsCollector.UpdateActiveCases(activeCases.Count);

            _logger.LogInformation("Retrieved {Count} active cases", activeCases.Count);
            return Result<List<SLAStatus>>.Success(activeCases);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetActiveCasesAsync cancelled");
            return ResultExtensions.Cancelled<List<SLAStatus>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active cases");
            return Result<List<SLAStatus>>.WithFailure($"Error retrieving active cases: {ex.Message}", default, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> EscalateCaseAsync(
        string fileId,
        EscalationLevel escalationLevel,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("EscalateCaseAsync cancelled before starting for file: {FileId}", fileId);
            return ResultExtensions.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            return Result.WithFailure("FileId cannot be null or empty");
        }

        try
        {
            var status = await _dbContext.SLAStatus
                .FirstOrDefaultAsync(s => s.FileId == fileId, cancellationToken).ConfigureAwait(false);

            if (status == null)
            {
                _logger.LogWarning("SLA status not found for escalation: {FileId}", fileId);
                return Result.WithFailure($"SLA status not found for file: {fileId}");
            }

            var previousLevel = status.EscalationLevel;
            status.EscalationLevel = escalationLevel;
            status.EscalatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _metricsCollector.RecordEscalation(escalationLevel);

            _logger.LogWarning("Case escalated: FileId={FileId}, PreviousLevel={PreviousLevel}, NewLevel={NewLevel}",
                fileId, previousLevel, escalationLevel);

            // Log escalation for audit trail (notification integration will be added separately)
            _logger.LogInformation("Escalation triggered: FileId={FileId}, Level={EscalationLevel}, Deadline={Deadline}",
                fileId, escalationLevel, status.Deadline);

            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("EscalateCaseAsync cancelled");
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error escalating case: {FileId}", fileId);
            return Result.WithFailure($"Error escalating case: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<int>> CalculateBusinessDaysAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("CalculateBusinessDaysAsync cancelled before starting");
            return ResultExtensions.Cancelled<int>();
        }

        try
        {
            var businessDays = CalculateBusinessDays(startDate, endDate);
            return Result<int>.Success(businessDays);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("CalculateBusinessDaysAsync cancelled");
            return ResultExtensions.Cancelled<int>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating business days");
            return Result<int>.WithFailure($"Error calculating business days: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Adds business days to a date, excluding weekends.
    /// </summary>
    /// <param name="startDate">The start date.</param>
    /// <param name="businessDays">The number of business days to add.</param>
    /// <returns>The date after adding business days.</returns>
    private static DateTime AddBusinessDays(DateTime startDate, int businessDays)
    {
        var currentDate = startDate;
        var daysAdded = 0;

        while (daysAdded < businessDays)
        {
            currentDate = currentDate.AddDays(1);
            // Skip weekends (Saturday = 6, Sunday = 0)
            if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
            {
                daysAdded++;
            }
        }

        return currentDate;
    }

    /// <summary>
    /// Calculates the number of business days between two dates, excluding weekends.
    /// </summary>
    /// <param name="startDate">The start date (inclusive).</param>
    /// <param name="endDate">The end date (exclusive).</param>
    /// <returns>The number of business days.</returns>
    private static int CalculateBusinessDays(DateTime startDate, DateTime endDate)
    {
        if (endDate < startDate)
        {
            return 0;
        }

        var businessDays = 0;
        var currentDate = startDate;

        // Count business days excluding the end date (exclusive end)
        while (currentDate < endDate)
        {
            // Count only weekdays (Monday-Friday)
            if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
            {
                businessDays++;
            }
            currentDate = currentDate.AddDays(1);
        }

        return businessDays;
    }

    /// <summary>
    /// Determines the escalation level based on remaining time and breach status.
    /// </summary>
    /// <param name="remainingTime">The time remaining until deadline.</param>
    /// <param name="isBreached">Whether the deadline has been breached.</param>
    /// <returns>The escalation level.</returns>
    private EscalationLevel DetermineEscalationLevel(TimeSpan remainingTime, bool isBreached)
    {
        if (isBreached)
        {
            return EscalationLevel.Breached;
        }

        if (remainingTime <= _options.CriticalThreshold)
        {
            return EscalationLevel.Critical;
        }

        if (remainingTime <= _options.WarningThreshold)
        {
            return EscalationLevel.Warning;
        }

        return EscalationLevel.None;
    }
}

