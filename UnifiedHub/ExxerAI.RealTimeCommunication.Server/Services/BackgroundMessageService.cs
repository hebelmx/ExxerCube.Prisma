using ExxerAI.RealTimeCommunication.Server.Hubs;
using ExxerAI.RealTimeCommunication.Server.Models;
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Hubs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExxerAI.RealTimeCommunication.Server.Services;

/// <summary>
/// Background service that periodically sends test messages to SignalR hubs.
/// Demonstrates server-side hub usage and serves as a test data generator.
/// </summary>
public class BackgroundMessageService : BackgroundService
{
    private readonly IExxerHub<SystemMessage> _systemHub;
    private readonly IExxerHub<HealthUpdate> _healthHub;
    private readonly SystemDiagnosticsCollector _diagnosticsCollector;
    private readonly ILogger<BackgroundMessageService> _logger;
    private int _messageCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundMessageService"/> class.
    /// </summary>
    /// <param name="systemHub">The system hub instance.</param>
    /// <param name="healthHub">The health hub instance.</param>
    /// <param name="logger">The logger instance.</param>
    public BackgroundMessageService(
        IExxerHub<SystemMessage> systemHub,
        IExxerHub<HealthUpdate> healthHub,
        SystemDiagnosticsCollector diagnosticsCollector,
        ILogger<BackgroundMessageService> logger)
    {
        _systemHub = systemHub ?? throw new ArgumentNullException(nameof(systemHub));
        _healthHub = healthHub ?? throw new ArgumentNullException(nameof(healthHub));
        _diagnosticsCollector = diagnosticsCollector ?? throw new ArgumentNullException(nameof(diagnosticsCollector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background message service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendSystemMessageAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                await SendHealthUpdateAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Background message service cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background message service");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Background message service stopped");
    }

    private async Task SendSystemMessageAsync(CancellationToken cancellationToken)
    {
        _messageCount++;
        var message = new SystemMessage
        {
            Content = $"Test message #{_messageCount}",
            Timestamp = DateTime.UtcNow,
            MessageType = "SystemNotification"
        };

        var result = await _systemHub.SendToAllAsync(message, cancellationToken);
        if (result.IsSuccess)
        {
            _logger.LogDebug("Sent system message #{MessageCount}", _messageCount);
        }
        else
        {
            _logger.LogWarning("Failed to send system message: {Error}", result.Error);
        }
    }

    private async Task SendHealthUpdateAsync(CancellationToken cancellationToken)
    {
        // Collect system diagnostics
        var diagnostics = _diagnosticsCollector.Collect();

        // Determine health status based on metrics
        var status = "Healthy";
        if (diagnostics.CpuUsagePercent > 90 || diagnostics.MemoryUsagePercent > 90)
        {
            status = "Degraded";
        }
        if (diagnostics.CpuUsagePercent > 95 || diagnostics.MemoryUsagePercent > 95)
        {
            status = "Unhealthy";
        }

        var healthUpdate = new HealthUpdate
        {
            ServiceName = "Server",
            Status = status,
            Timestamp = DateTime.UtcNow,
            Data = new Dictionary<string, object>
            {
                { "CpuUsagePercent", diagnostics.CpuUsagePercent },
                { "MemoryUsageMB", diagnostics.MemoryUsageMB },
                { "TotalMemoryMB", diagnostics.TotalMemoryMB },
                { "MemoryUsagePercent", diagnostics.MemoryUsagePercent },
                { "ProcessCount", diagnostics.ProcessCount },
                { "ThreadCount", diagnostics.ThreadCount },
                { "MachineName", diagnostics.MachineName }
            }
        };

        var result = await _healthHub.SendToAllAsync(healthUpdate, cancellationToken);
        if (result.IsSuccess)
        {
            _logger.LogDebug("Sent health update: CPU={Cpu}%, Memory={Memory}MB ({MemoryPercent}%)",
                diagnostics.CpuUsagePercent, diagnostics.MemoryUsageMB, diagnostics.MemoryUsagePercent);
        }
        else
        {
            _logger.LogWarning("Failed to send health update: {Error}", result.Error);
        }
    }
}

