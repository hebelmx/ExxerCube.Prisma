namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Orchestrates SLA tracking and escalation management for regulatory response cases.
/// </summary>
public class SLATrackingService
{
    private readonly ISLAEnforcer _slaEnforcer;
    private readonly ILogger<SLATrackingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SLATrackingService"/> class.
    /// </summary>
    /// <param name="slaEnforcer">The SLA enforcer service.</param>
    /// <param name="logger">The logger instance.</param>
    public SLATrackingService(
        ISLAEnforcer slaEnforcer,
        ILogger<SLATrackingService> logger)
    {
        _slaEnforcer = slaEnforcer ?? throw new ArgumentNullException(nameof(slaEnforcer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Tracks SLA for a file based on intake date and days plazo from expediente.
    /// </summary>
    /// <param name="fileId">The file identifier.</param>
    /// <param name="intakeDate">The date when the file was received.</param>
    /// <param name="daysPlazo">The number of business days granted for compliance (from expediente).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the SLA status or an error.</returns>
    public async Task<Result<SLAStatus>> TrackSLAAsync(
        string fileId,
        DateTime intakeDate,
        int daysPlazo,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("SLA tracking cancelled before starting for file: {FileId}", fileId);
            return ResultExtensions.Cancelled<SLAStatus>();
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            return Result<SLAStatus>.WithFailure("FileId cannot be null or empty");
        }

        if (daysPlazo <= 0)
        {
            return Result<SLAStatus>.WithFailure("DaysPlazo must be greater than zero");
        }

        try
        {
            _logger.LogInformation("Tracking SLA for file: {FileId}, intake date: {IntakeDate}, days plazo: {DaysPlazo}",
                fileId, intakeDate, daysPlazo);

            var result = await _slaEnforcer.CalculateSLAStatusAsync(fileId, intakeDate, daysPlazo, cancellationToken).ConfigureAwait(false);

            if (result.IsCancelled())
            {
                _logger.LogWarning("SLA tracking cancelled during calculation");
                return ResultExtensions.Cancelled<SLAStatus>();
            }

            if (result.IsFailure)
            {
                _logger.LogError("Failed to track SLA: {Error}", result.Error);
                return Result<SLAStatus>.WithFailure($"Failed to track SLA: {result.Error}");
            }

            var slaStatus = result.Value;
            if (slaStatus != null && slaStatus.IsAtRisk)
            {
                _logger.LogWarning("File at risk: {FileId}, remaining time: {RemainingTime}, escalation: {EscalationLevel}",
                    fileId, slaStatus.RemainingTime, slaStatus.EscalationLevel);
            }

            return Result<SLAStatus>.Success(slaStatus!);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("SLA tracking cancelled");
            return ResultExtensions.Cancelled<SLAStatus>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking SLA for file: {FileId}", fileId);
            return Result<SLAStatus>.WithFailure($"Error tracking SLA: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Updates SLA status for a file, recalculating deadline and escalation level.
    /// </summary>
    /// <param name="fileId">The file identifier.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the updated SLA status or an error.</returns>
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

        try
        {
            var result = await _slaEnforcer.UpdateSLAStatusAsync(fileId, cancellationToken).ConfigureAwait(false);

            if (result.IsCancelled())
            {
                return ResultExtensions.Cancelled<SLAStatus>();
            }

            if (result.IsFailure)
            {
                return Result<SLAStatus>.WithFailure($"Failed to update SLA status: {result.Error}");
            }

            if (result.IsSuccess && result.Value is not null)
            {
                return Result<SLAStatus>.Success(result.Value);
            }

            // Fallback: should not happen, but handle gracefully
            return Result<SLAStatus>.WithFailure("SLA status update returned null value");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("SLA status update cancelled");
            return ResultExtensions.Cancelled<SLAStatus>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SLA status for file: {FileId}", fileId);
            return Result<SLAStatus>.WithFailure($"Error updating SLA status: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Gets all active cases with their SLA status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of active SLA statuses or an error.</returns>
    public async Task<Result<List<SLAStatus>>> GetActiveCasesAsync(
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetActiveCasesAsync cancelled before starting");
            return ResultExtensions.Cancelled<List<SLAStatus>>();
        }

        try
        {
            var result = await _slaEnforcer.GetActiveCasesAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsCancelled())
            {
                return ResultExtensions.Cancelled<List<SLAStatus>>();
            }

            if (result.IsFailure)
            {
                return Result<List<SLAStatus>>.WithFailure($"Failed to get active cases: {result.Error}");
            }

            return Result<List<SLAStatus>>.Success(result.Value ?? new List<SLAStatus>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetActiveCasesAsync cancelled");
            return ResultExtensions.Cancelled<List<SLAStatus>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active cases");
            return Result<List<SLAStatus>>.WithFailure($"Error getting active cases: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Gets all cases at risk (within critical threshold).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of at-risk SLA statuses or an error.</returns>
    public async Task<Result<List<SLAStatus>>> GetAtRiskCasesAsync(
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetAtRiskCasesAsync cancelled before starting");
            return ResultExtensions.Cancelled<List<SLAStatus>>();
        }

        try
        {
            var result = await _slaEnforcer.GetAtRiskCasesAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsCancelled())
            {
                return ResultExtensions.Cancelled<List<SLAStatus>>();
            }

            if (result.IsFailure)
            {
                return Result<List<SLAStatus>>.WithFailure($"Failed to get at-risk cases: {result.Error}");
            }

            return Result<List<SLAStatus>>.Success(result.Value ?? new List<SLAStatus>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetAtRiskCasesAsync cancelled");
            return ResultExtensions.Cancelled<List<SLAStatus>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting at-risk cases");
            return Result<List<SLAStatus>>.WithFailure($"Error getting at-risk cases: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Gets all cases that have breached their deadline.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of breached SLA statuses or an error.</returns>
    public async Task<Result<List<SLAStatus>>> GetBreachedCasesAsync(
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetBreachedCasesAsync cancelled before starting");
            return ResultExtensions.Cancelled<List<SLAStatus>>();
        }

        try
        {
            var result = await _slaEnforcer.GetBreachedCasesAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsCancelled())
            {
                return ResultExtensions.Cancelled<List<SLAStatus>>();
            }

            if (result.IsFailure)
            {
                return Result<List<SLAStatus>>.WithFailure($"Failed to get breached cases: {result.Error}");
            }

            return Result<List<SLAStatus>>.Success(result.Value ?? new List<SLAStatus>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetBreachedCasesAsync cancelled");
            return ResultExtensions.Cancelled<List<SLAStatus>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting breached cases");
            return Result<List<SLAStatus>>.WithFailure($"Error getting breached cases: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Escalates a case to the specified escalation level.
    /// </summary>
    /// <param name="fileId">The file identifier.</param>
    /// <param name="escalationLevel">The escalation level to set.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
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
            var result = await _slaEnforcer.EscalateCaseAsync(fileId, escalationLevel, cancellationToken).ConfigureAwait(false);

            if (result.IsCancelled())
            {
                return ResultExtensions.Cancelled();
            }

            if (result.IsFailure)
            {
                return Result.WithFailure($"Failed to escalate case: {result.Error}");
            }

            _logger.LogWarning("Case escalated: FileId={FileId}, Level={EscalationLevel}", fileId, escalationLevel);
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
}