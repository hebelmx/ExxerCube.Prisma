using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SnakeWorker.Core.Interfaces;
using SnakeWorker.Core.Services;
using SnakeWorker.Python;
using System.Diagnostics;

namespace SnakeWorker.Host;

/// <summary>
/// Main program entry point
/// </summary>
public class Program
{
    /// <summary>
    /// Main entry point
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Exit code</returns>
    public static async Task<int> Main(string[] args)
    {
        // Configure Serilog early
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting SnakeWorker Host");

            var builder = CreateHostBuilder(args);
            var host = builder.Build();

            // Log startup information
            await LogStartupInfo(host);

            await host.RunAsync();
            
            Log.Information("SnakeWorker Host shut down gracefully");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "SnakeWorker Host terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Create and configure the host builder
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Configured host builder</returns>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "SnakeWorker";
            })
            .UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("logs/snakeworker-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30))
            .ConfigureServices((hostContext, services) =>
            {
                var configuration = hostContext.Configuration;

                // Configure options
                services.Configure<WorkerOptions>(
                    configuration.GetSection(WorkerOptions.SectionName));
                services.Configure<CSnakesOptions>(
                    configuration.GetSection(CSnakesOptions.SectionName));

                // Register services
                services.AddSingleton<ITaskQueue, InMemoryTaskQueue>();
                services.AddSingleton<IMetricsCollector, MetricsCollector>();
                services.AddSingleton<IPythonExecutor, CSnakesPythonExecutor>();

                // Register background service
                services.AddHostedService<BackgroundWorkerService>();

                // Register additional services
                services.AddSingleton<HealthService>();
                services.AddSingleton<MetricsReportingService>();
                services.AddHostedService<MetricsReportingService>();

                // Add health checks
                services.AddHealthChecks()
                    .AddCheck<PythonHealthCheck>("python")
                    .AddCheck<QueueHealthCheck>("queue");
            });

    /// <summary>
    /// Log startup information
    /// </summary>
    /// <param name="host">The configured host</param>
    private static async Task LogStartupInfo(IHost host)
    {
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("=== SnakeWorker Startup Information ===");
        logger.LogInformation("Process ID: {ProcessId}", Environment.ProcessId);
        logger.LogInformation("Machine Name: {MachineName}", Environment.MachineName);
        logger.LogInformation("OS Version: {OSVersion}", Environment.OSVersion);
        logger.LogInformation("CLR Version: {CLRVersion}", Environment.Version);
        logger.LogInformation("Working Directory: {WorkingDirectory}", Environment.CurrentDirectory);
        logger.LogInformation("Processor Count: {ProcessorCount}", Environment.ProcessorCount);

        // Log Python environment info
        try
        {
            var pythonExecutor = host.Services.GetRequiredService<IPythonExecutor>();
            var pythonInfo = await pythonExecutor.GetEnvironmentInfoAsync();
            
            logger.LogInformation("Python Environment:");
            foreach (var kvp in pythonInfo)
            {
                logger.LogInformation("  {Key}: {Value}", kvp.Key, kvp.Value);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get Python environment information");
        }

        // Log configuration
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var workerOptions = new WorkerOptions();
        configuration.GetSection(WorkerOptions.SectionName).Bind(workerOptions);
        
        logger.LogInformation("Worker Configuration:");
        logger.LogInformation("  Concurrent Workers: {ConcurrentWorkers}", workerOptions.ConcurrentWorkers);
        logger.LogInformation("  Polling Interval: {PollingIntervalMs}ms", workerOptions.PollingIntervalMs);
        logger.LogInformation("  Default Max Retries: {DefaultMaxRetries}", workerOptions.DefaultMaxRetries);
        logger.LogInformation("  Default Timeout: {DefaultTimeoutSeconds}s", workerOptions.DefaultTimeoutSeconds);

        logger.LogInformation("=== Startup Complete ===");
    }
}

/// <summary>
/// Health service for monitoring application health
/// </summary>
public class HealthService
{
    private readonly ILogger<HealthService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public HealthService(ILogger<HealthService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Get health status
    /// </summary>
    /// <returns>Health information</returns>
    public async Task<Dictionary<string, object>> GetHealthAsync()
    {
        var health = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow,
            ["uptime"] = DateTime.UtcNow - Process.GetCurrentProcess().StartTime,
            ["status"] = "healthy"
        };

        try
        {
            // Check queue health
            var taskQueue = _serviceProvider.GetRequiredService<ITaskQueue>();
            var queueSize = await taskQueue.GetQueueSizeAsync();
            health["queue_size"] = queueSize;

            // Check Python health
            var pythonExecutor = _serviceProvider.GetRequiredService<IPythonExecutor>();
            var pythonHealthy = await pythonExecutor.IsModuleAvailableAsync("sys");
            health["python_available"] = pythonHealthy;

            // Get metrics snapshot
            var metricsCollector = _serviceProvider.GetRequiredService<IMetricsCollector>();
            var metrics = metricsCollector.GetMetricsSnapshot();
            health["metrics"] = metrics;

            // Memory information
            var gc = GC.GetTotalMemory(false);
            health["memory_usage_bytes"] = gc;

            _logger.LogDebug("Health check completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            health["status"] = "unhealthy";
            health["error"] = ex.Message;
        }

        return health;
    }
}