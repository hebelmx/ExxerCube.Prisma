using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Infrastructure.Database.Services;

/// <summary>
/// Background service that periodically updates SLA statuses and triggers automatic escalations.
/// </summary>
public class SLAUpdateBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SLAUpdateBackgroundService> _logger;
    private readonly SLAUpdateOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SLAUpdateBackgroundService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The SLA update configuration options.</param>
    public SLAUpdateBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SLAUpdateBackgroundService> logger,
        IOptions<SLAUpdateOptions> options)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SLA Update Background Service started. Update interval: {IntervalSeconds}s, Batch size: {BatchSize}",
            _options.UpdateIntervalSeconds, _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateSLAStatusesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("SLA Update Background Service cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SLA Update Background Service. Will retry after delay.");
                
                // Wait before retrying to avoid tight error loops
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            // Wait for the configured interval before next update cycle
            if (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.UpdateIntervalSeconds), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("SLA Update Background Service stopped");
    }

    /// <summary>
    /// Updates SLA statuses in batches and triggers automatic escalations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    private async Task UpdateSLAStatusesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var slaEnforcer = scope.ServiceProvider.GetRequiredService<ISLAEnforcer>();

        // Get all active SLA cases
        var activeCasesResult = await slaEnforcer.GetActiveCasesAsync(cancellationToken).ConfigureAwait(false);

        if (activeCasesResult.IsCancelled())
        {
            _logger.LogInformation("SLA status update cancelled");
            return;
        }

        if (activeCasesResult.IsFailure)
        {
            _logger.LogError("Failed to retrieve active SLA cases: {Error}", activeCasesResult.Error);
            return;
        }

        var activeCases = activeCasesResult.Value ?? Enumerable.Empty<ExxerCube.Prisma.Domain.Entities.SLAStatus>();
        var totalCases = activeCases.Count();

        if (totalCases == 0)
        {
            _logger.LogDebug("No active SLA cases to update");
            return;
        }

        _logger.LogInformation("Updating {Count} active SLA cases in batches of {BatchSize}", totalCases, _options.BatchSize);

        var processedCount = 0;
        var successCount = 0;
        var failureCount = 0;
        var escalatedCount = 0;

        // Process in batches
        var batches = activeCases
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / _options.BatchSize)
            .Select(g => g.Select(x => x.item).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("SLA status update cancelled during batch processing");
                break;
            }

            var batchTasks = batch.Select(async slaStatus =>
            {
                try
                {
                    // Update SLA status
                    var updateResult = await slaEnforcer.UpdateSLAStatusAsync(slaStatus.FileId, cancellationToken).ConfigureAwait(false);

                    if (updateResult.IsCancelled())
                    {
                        return (Success: false, Escalated: false, Cancelled: true);
                    }

                    if (updateResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to update SLA status for file {FileId}: {Error}", slaStatus.FileId, updateResult.Error);
                        return (Success: false, Escalated: false, Cancelled: false);
                    }

                    var updatedStatus = updateResult.Value;
                    if (updatedStatus == null)
                    {
                        return (Success: false, Escalated: false, Cancelled: false);
                    }

                    // Check if escalation was triggered
                    var wasEscalated = updatedStatus.EscalationLevel != EscalationLevel.None &&
                                      updatedStatus.EscalatedAt.HasValue;

                    if (wasEscalated)
                    {
                        _logger.LogWarning(
                            "SLA escalation triggered for file {FileId}: Level={Level}, RemainingTime={RemainingTime}",
                            updatedStatus.FileId, updatedStatus.EscalationLevel, updatedStatus.RemainingTime);
                    }

                    return (Success: true, Escalated: wasEscalated, Cancelled: false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return (Success: false, Escalated: false, Cancelled: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception updating SLA status for file {FileId}", slaStatus.FileId);
                    return (Success: false, Escalated: false, Cancelled: false);
                }
            });

            var batchResults = await Task.WhenAll(batchTasks).ConfigureAwait(false);

            foreach (var result in batchResults)
            {
                processedCount++;
                if (result.Cancelled)
                {
                    break;
                }
                if (result.Success)
                {
                    successCount++;
                    if (result.Escalated)
                    {
                        escalatedCount++;
                    }
                }
                else
                {
                    failureCount++;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation(
            "SLA status update cycle completed: Processed={Processed}, Success={Success}, Failed={Failed}, Escalated={Escalated}",
            processedCount, successCount, failureCount, escalatedCount);
    }
}

