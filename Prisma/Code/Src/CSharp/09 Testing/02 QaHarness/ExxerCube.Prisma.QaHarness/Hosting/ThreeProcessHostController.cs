// <copyright file="ThreeProcessHostController.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Diagnostics;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Worker.Ingestion;
using Prisma.Orion.Ingestion;
using Prisma.Reconciliator.Worker;

namespace ExxerCube.Prisma.QaHarness.Hosting;

/// <summary>
/// Application host controller that boots the three-process pipeline:
/// <list type="bullet">
///   <item><description>Orion Worker (document downloader / SIARA discovery)</description></item>
///   <item><description>Athena Worker (extraction pipeline)</description></item>
///   <item><description>Reconciliator Worker (classification + export)</description></item>
/// </list>
/// Replicates the <c>BuildThreeHostsWithDb</c> technique from
/// <c>MaxFidelityGateE2EBase</c>: each worker's <c>Program.cs</c> reads
/// <c>ConnectionStrings__DefaultConnection</c> (double-underscore env-var form) eagerly
/// during <c>WebApplication.CreateBuilder</c>, before WAF's <c>ConfigureAppConfiguration</c>
/// runs.  The env-var is set for the build window and then restored to avoid leaking to other
/// hosts.
/// </summary>
/// <remarks>
/// Activates for <see cref="HostingMode.ThreeProcessPipeline"/> and <see cref="HostingMode.All"/>.
/// </remarks>
public sealed class ThreeProcessHostController : IApplicationHostController
{
    private const string ConnEnvVar = "ConnectionStrings__DefaultConnection";

    private readonly ILogger<ThreeProcessHostController> _logger;

    private HarnessOrionApp? _orionApp;
    private HarnessAthenaApp? _athenaApp;
    private HarnessReconciliatorApp? _reconciliatorApp;
    private bool _disposed;

    /// <summary>
    /// Initialises a new instance of <see cref="ThreeProcessHostController"/>.
    /// </summary>
    /// <param name="logger">Logger for startup diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    public ThreeProcessHostController(ILogger<ThreeProcessHostController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initialises a new instance of <see cref="ThreeProcessHostController"/> using a null logger.
    /// Suitable for tests that do not require structured output.
    /// </summary>
    public ThreeProcessHostController()
        : this(NullLogger<ThreeProcessHostController>.Instance)
    {
    }

    /// <summary>
    /// Gets the DI service provider of the Orion Worker host (the primary process).
    /// <see langword="null"/> before a successful <see cref="StartAsync"/>.
    /// </summary>
    public IServiceProvider? Services => _orionApp?.Services;

    /// <summary>
    /// Gets the DI service provider of the Athena Worker host.
    /// <see langword="null"/> before a successful <see cref="StartAsync"/>.
    /// </summary>
    public IServiceProvider? AthenaServices => _athenaApp?.Services;

    /// <summary>
    /// Gets the DI service provider of the Reconciliator Worker host.
    /// <see langword="null"/> before a successful <see cref="StartAsync"/>.
    /// </summary>
    public IServiceProvider? ReconciliatorServices => _reconciliatorApp?.Services;

    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="ThreeProcessHostController"/> does not expose a single Kestrel base address
    /// (none of the workers bind an HTTP port by default).  This property always returns
    /// <see langword="null"/>.
    /// </remarks>
    public Uri? BaseAddress => null;

    /// <inheritdoc/>
    public async Task<Result<ApplicationStartupResult>> StartAsync(
        HostingOptions options,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<ApplicationStartupResult>.WithFailure("StartAsync cancelled before it began.");
        }

        if (options is null)
        {
            return Result<ApplicationStartupResult>.WithFailure("HostingOptions must not be null.");
        }

        if (options.Mode is not (HostingMode.ThreeProcessPipeline or HostingMode.All))
        {
            return Result<ApplicationStartupResult>.WithFailure(
                $"ThreeProcessHostController does not handle HostingMode.{options.Mode}. " +
                "Use PrismaWebUiHostController for WebUiOnly.");
        }

        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "ThreeProcessHostController: starting three-process pipeline (SQL={HasSql}, Storage={HasStorage})",
                options.SqlConnectionString is not null,
                options.SharedStoragePath is not null);

            await Task.Run(
                () => BuildThreeHosts(options),
                cancellationToken).ConfigureAwait(false);

            sw.Stop();

            // ── Verify that all three hosts actually started ────────────────
            // Accessing .Services triggers WAF lazy-boot; null means the host never
            // materialised (BuildThreeHosts would normally throw, but guard defensively).
            var failureReason = VerifyHostsStarted();
            var isHealthy = failureReason is null;

            if (!isHealthy)
            {
                _logger.LogWarning(
                    "ThreeProcessHostController: startup completed but health verification failed after {ElapsedMs} ms: {Reason}",
                    sw.ElapsedMilliseconds,
                    failureReason);
            }
            else
            {
                _logger.LogInformation(
                    "ThreeProcessHostController: all three hosts started and verified in {ElapsedMs} ms.",
                    sw.ElapsedMilliseconds);
            }

            var result = new ApplicationStartupResult(
                IsHealthy: isHealthy,
                BaseAddress: null,   // workers do not expose a browser-reachable address
                StartupDurationMs: sw.ElapsedMilliseconds,
                FailureReason: failureReason);

            return Result<ApplicationStartupResult>.WithSuccess(result);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return Result<ApplicationStartupResult>.WithFailure("StartAsync was cancelled during host boot.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "ThreeProcessHostController: startup failed after {ElapsedMs} ms.",
                sw.ElapsedMilliseconds);
            return Result<ApplicationStartupResult>.WithFailure(
                $"Three-process startup threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result> StopAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result.WithFailure("StopAsync cancelled before it began.");
        }

        var errors = new List<string>();

        await DisposeHostAsync(_reconciliatorApp, "Reconciliator", errors).ConfigureAwait(false);
        _reconciliatorApp = null;

        await DisposeHostAsync(_athenaApp, "Athena", errors).ConfigureAwait(false);
        _athenaApp = null;

        await DisposeHostAsync(_orionApp, "Orion", errors).ConfigureAwait(false);
        _orionApp = null;

        if (errors.Count > 0)
        {
            return Result.WithFailure(string.Join("; ", errors));
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    // ── Post-boot health verification ─────────────────────────────────────────────

    /// <summary>
    /// Verifies that all three hosts started successfully by asserting their service providers
    /// are non-null (a null provider means WAF never completed host boot).
    /// </summary>
    /// <returns>
    /// <see langword="null"/> when all three hosts have a valid service provider; otherwise a
    /// human-readable failure reason that describes which host(s) failed to start.
    /// </returns>
    private string? VerifyHostsStarted()
    {
        var missing = new List<string>(3);

        if (_orionApp?.Services is null)
            missing.Add("Orion");

        if (_athenaApp?.Services is null)
            missing.Add("Athena");

        if (_reconciliatorApp?.Services is null)
            missing.Add("Reconciliator");

        return missing.Count > 0
            ? $"Host service provider(s) are null after boot — failed worker(s): {string.Join(", ", missing)}."
            : null;
    }

    // ── Three-host boot (mirrors BuildThreeHostsWithDb) ──────────────────────────

    private void BuildThreeHosts(HostingOptions options)
    {
        // Workers read ConnectionStrings__DefaultConnection EAGERLY from the environment during
        // WebApplication.CreateBuilder (before WAF ConfigureAppConfiguration runs).  Set the env-var
        // for the build window only, then restore it to avoid leaking to other concurrent tests.
        var previousConn = Environment.GetEnvironmentVariable(ConnEnvVar);
        if (options.SqlConnectionString is not null)
        {
            Environment.SetEnvironmentVariable(ConnEnvVar, options.SqlConnectionString);
        }

        try
        {
            _orionApp = new HarnessOrionApp(options);
            // Accessing .Services triggers WAF host boot (lazy initialisation on first access).
            _ = _orionApp.Services;

            _athenaApp = new HarnessAthenaApp(options, _orionApp);
            _ = _athenaApp.Services;

            _reconciliatorApp = new HarnessReconciliatorApp(options, _athenaApp);
            _ = _reconciliatorApp.Services;
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnEnvVar, previousConn);
        }
    }

    private async Task DisposeHostAsync(IAsyncDisposable? host, string name, List<string> errors)
    {
        if (host is null)
        {
            return;
        }

        try
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ThreeProcessHostController: error disposing {HostName} host.", name);
            errors.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ── Inner host factories ──────────────────────────────────────────────────────

    /// <summary>
    /// Harness-owned Orion Worker WAF: real SIARA download path, autonomous watch loop optionally
    /// disabled, SQL connection injected via in-memory configuration.
    /// </summary>
    private sealed class HarnessOrionApp : WebApplicationFactory<global::Prisma.Orion.Worker.Program>
    {
        private readonly HostingOptions _options;

        internal HarnessOrionApp(HostingOptions options)
        {
            _options = options;
        }

        /// <inheritdoc/>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var overrides = BuildConfigOverrides(_options, "orion-harness", "Orion (Harness)", "Download");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(overrides));

            builder.ConfigureServices(services =>
            {
                if (_options.DisableAutonomousWatchLoop)
                {
                    // Remove the background OrionWorkerService so tests control discovery explicitly.
                    var watchLoop = services
                        .Where(s => s.ImplementationType?.Name == "OrionWorkerService")
                        .ToList();
                    foreach (var d in watchLoop)
                    {
                        services.Remove(d);
                    }
                }
            });
        }
    }

    /// <summary>
    /// Harness-owned Athena Worker WAF: extraction pipeline (FileSystemLoader → quality →
    /// Tesseract → fusion), SignalR client routed through Orion's TestServer (in-memory transport).
    /// </summary>
    private sealed class HarnessAthenaApp : WebApplicationFactory<global::Prisma.Athena.Worker.Program>
    {
        private readonly HostingOptions _options;
        private readonly HarnessOrionApp _orionApp;

        internal HarnessAthenaApp(HostingOptions options, HarnessOrionApp orionApp)
        {
            _options = options;
            _orionApp = orionApp;
        }

        /// <inheritdoc/>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var overrides = BuildConfigOverrides(_options, "athena-harness", "Athena (Harness)", "Extract");
            overrides["Ingestion:HubUrl"] = "http://orion-testserver/hubs/ingestion";
            overrides["Ingestion:ReconnectDelay"] = "00:00:00.200";

            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(overrides));

            builder.ConfigureServices(services =>
            {
                // Route the production SiaraIngestionHubClient through Orion's TestServer handler.
                services.Configure<IngestionClientOptions>(o =>
                    o.HttpMessageHandlerFactory = () => _orionApp.Server.CreateHandler());
            });
        }
    }

    /// <summary>
    /// Harness-owned Reconciliator Worker WAF: real classification + SIRO export + SQL audit.
    /// SignalR client routed through Athena's TestServer (in-memory transport).
    /// Exposes its <see cref="EventPublisher"/> so callers can subscribe to terminal events.
    /// </summary>
    private sealed class HarnessReconciliatorApp : WebApplicationFactory<global::Prisma.Reconciliator.Worker.Program>
    {
        private readonly HostingOptions _options;
        private readonly HarnessAthenaApp _athenaApp;

        /// <summary>
        /// Observable event publisher for the Reconciliator host.
        /// Subscribe here to receive <c>ExportCompletedEvent</c> and other terminal domain events.
        /// </summary>
        internal readonly EventPublisher ReconciliatorEventPublisher =
            new(NullLogger<EventPublisher>.Instance);

        internal HarnessReconciliatorApp(HostingOptions options, HarnessAthenaApp athenaApp)
        {
            _options = options;
            _athenaApp = athenaApp;
        }

        /// <inheritdoc/>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var overrides = BuildConfigOverrides(_options, "reconciliator-harness", "Reconciliator (Harness)", "Reconcile");
            overrides["Reconciliation:HubUrl"] = "http://athena-testserver/hubs/reconciliation";
            overrides["Reconciliation:ReconnectDelay"] = "00:00:00.200";

            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(overrides));

            builder.ConfigureServices(services =>
            {
                services.Configure<ReconciliationClientOptions>(o =>
                    o.HttpMessageHandlerFactory = () => _athenaApp.Server.CreateHandler());

                // Replace IEventPublisher with our observable instance so tests can subscribe.
                services.RemoveAll<IEventPublisher>();
                services.AddSingleton<IEventPublisher>(ReconciliatorEventPublisher);
            });
        }
    }

    // ── Config-override builder shared by all three inner factories ───────────────

    private static Dictionary<string, string?> BuildConfigOverrides(
        HostingOptions options,
        string actorId,
        string displayName,
        string clearance)
    {
        var d = new Dictionary<string, string?>
        {
            ["Siara:Actor:ActorId"]          = actorId,
            ["Siara:Actor:DisplayName"]      = displayName,
            ["ProcessIdentity:JwtIssuer"]    = "prisma-pipeline",
            ["ProcessIdentity:JwtAudience"]  = "prisma-pipeline",
            ["ProcessIdentity:TokenLifetime"] = "01:00:00",
            ["ProcessIdentity:Clearance"]    = clearance,
        };

        if (options.SqlConnectionString is not null)
        {
            d["ConnectionStrings:DefaultConnection"] = options.SqlConnectionString;
        }

        if (options.SharedStoragePath is not null)
        {
            d["Storage:BasePath"] = options.SharedStoragePath;
        }

        if (options.SiaraStorageState is not null)
        {
            d["Siara:AuthMode"]                         = "SessionPassthrough";
            d["Siara:Passthrough:Transport"]            = "StorageState";
            d["Siara:Passthrough:StorageStateRef"]      = options.SiaraStorageState;
            d["Siara:Passthrough:DashboardUrl"]         = "http://localhost:5001/";
            d["Siara:Passthrough:PostLoginSelector"]    = "#arrivalRateSlider";
            d["NavigationTargets:SiaraUrl"]             = "http://localhost:5001/";
            d["BrowserAutomation:Headless"]             = "true";
            d["BrowserAutomation:IgnoreHttpsErrors"]    = "true";
            d["BrowserAutomation:PageTimeoutMs"]        = "60000";
        }

        return d;
    }
}
