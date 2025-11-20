using ExxerAI.RealTimeCommunication.Client.Dashboards;
using ExxerAI.RealTimeCommunication.Client.Health;
using ExxerAI.RealTimeCommunication.Server.Hubs;
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Dashboards;
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Health;
using ExxerCube.Prisma.SignalR.Abstractions.Infrastructure.Connection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExxerAI.RealTimeCommunication.Client.Services;

/// <summary>
/// Background service that demonstrates dashboard and health monitoring usage.
/// Connects to dashboards, monitors health, and logs activity.
/// </summary>
public class DashboardBackgroundService : BackgroundService
{
    private readonly IDashboard<SystemMessage> _systemDashboard;
    private readonly IDashboard<HealthUpdate> _healthDashboard;
    private readonly IServiceHealth<HealthStatusData> _clientHealth;
    private readonly ILogger<DashboardBackgroundService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardBackgroundService"/> class.
    /// </summary>
    /// <param name="systemDashboard">The system dashboard.</param>
    /// <param name="healthDashboard">The health dashboard.</param>
    /// <param name="clientHealth">The client health monitor.</param>
    /// <param name="logger">The logger instance.</param>
    public DashboardBackgroundService(
        IDashboard<SystemMessage> systemDashboard,
        IDashboard<HealthUpdate> healthDashboard,
        IServiceHealth<HealthStatusData> clientHealth,
        ILogger<DashboardBackgroundService> logger)
    {
        _systemDashboard = systemDashboard ?? throw new ArgumentNullException(nameof(systemDashboard));
        _healthDashboard = healthDashboard ?? throw new ArgumentNullException(nameof(healthDashboard));
        _clientHealth = clientHealth ?? throw new ArgumentNullException(nameof(clientHealth));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dashboard background service started");

        // Connect dashboards
        var systemConnectResult = await _systemDashboard.ConnectAsync(stoppingToken);
        if (systemConnectResult.IsFailure)
        {
            _logger.LogError("Failed to connect system dashboard: {Error}", systemConnectResult.Error);
        }
        else
        {
            _logger.LogInformation("System dashboard connected");
        }

        var healthConnectResult = await _healthDashboard.ConnectAsync(stoppingToken);
        if (healthConnectResult.IsFailure)
        {
            _logger.LogError("Failed to connect health dashboard: {Error}", healthConnectResult.Error);
        }
        else
        {
            _logger.LogInformation("Health dashboard connected");
        }

        // Subscribe to events
        _systemDashboard.DataReceived += OnSystemMessageReceived;
        _systemDashboard.ConnectionStateChanged += OnSystemConnectionStateChanged;

        _healthDashboard.DataReceived += OnHealthUpdateReceived;
        _healthDashboard.ConnectionStateChanged += OnHealthConnectionStateChanged;

        _clientHealth.HealthStatusChanged += OnClientHealthChanged;

        // Update client health periodically
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Collect system diagnostics
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var workingSetMB = process.WorkingSet64 / 1024 / 1024;
                var totalMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
                var memoryUsagePercent = totalMemoryMB > 0 ? (workingSetMB * 100.0 / totalMemoryMB) : 0;

                // Simple CPU calculation (process-based)
                var cpuTime = process.TotalProcessorTime.TotalMilliseconds;
                await Task.Delay(100, stoppingToken); // Small delay for CPU calculation
                var cpuTimeAfter = process.TotalProcessorTime.TotalMilliseconds;
                var cpuUsage = Math.Min(100, Math.Max(0, (cpuTimeAfter - cpuTime) / 100.0 * 100));

                var healthData = new HealthStatusData
                {
                    ClientId = Environment.MachineName,
                    ConnectionCount = _systemDashboard.ConnectionState == ConnectionState.Connected ? 1 : 0,
                    LastActivity = DateTime.UtcNow,
                    CpuUsagePercent = cpuUsage,
                    MemoryUsageMB = workingSetMB,
                    TotalMemoryMB = totalMemoryMB,
                    MemoryUsagePercent = memoryUsagePercent,
                    ProcessCount = System.Diagnostics.Process.GetProcesses().Length,
                    ThreadCount = process.Threads.Count
                };

                // Determine health status based on metrics
                var status = HealthStatus.Healthy;
                if (_systemDashboard.ConnectionState != ConnectionState.Connected)
                {
                    status = HealthStatus.Unhealthy;
                }
                else if (cpuUsage > 90 || memoryUsagePercent > 90)
                {
                    status = HealthStatus.Degraded;
                }
                else if (cpuUsage > 95 || memoryUsagePercent > 95)
                {
                    status = HealthStatus.Unhealthy;
                }

                await _clientHealth.UpdateHealthAsync(status, healthData, stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating client health");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        // Cleanup
        _systemDashboard.DataReceived -= OnSystemMessageReceived;
        _systemDashboard.ConnectionStateChanged -= OnSystemConnectionStateChanged;
        _healthDashboard.DataReceived -= OnHealthUpdateReceived;
        _healthDashboard.ConnectionStateChanged -= OnHealthConnectionStateChanged;
        _clientHealth.HealthStatusChanged -= OnClientHealthChanged;

        await _systemDashboard.DisconnectAsync(stoppingToken);
        await _healthDashboard.DisconnectAsync(stoppingToken);

        _logger.LogInformation("Dashboard background service stopped");
    }

    private void OnSystemMessageReceived(object? sender, DataReceivedEventArgs<SystemMessage> e)
    {
        _logger.LogInformation("System message received: {Content} ({Type})", e.Data.Content, e.Data.MessageType);
    }

    private void OnSystemConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        _logger.LogInformation("System dashboard connection state changed: {PreviousState} -> {NewState}",
            e.PreviousState, e.NewState);
    }

    private void OnHealthUpdateReceived(object? sender, DataReceivedEventArgs<HealthUpdate> e)
    {
        _logger.LogInformation("Health update received: {ServiceName} - {Status}", e.Data.ServiceName, e.Data.Status);
    }

    private void OnHealthConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        _logger.LogInformation("Health dashboard connection state changed: {PreviousState} -> {NewState}",
            e.PreviousState, e.NewState);
    }

    private void OnClientHealthChanged(object? sender, HealthStatusChangedEventArgs<HealthStatusData> e)
    {
        _logger.LogInformation("Client health changed: {PreviousStatus} -> {NewStatus}", e.PreviousStatus, e.NewStatus);
    }
}

