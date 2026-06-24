using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
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

namespace ExxerCube.Prisma.Tests.AllRealWireE2E;

// ── Max-fidelity gate (#5) worker hosts ───────────────────────────────────────────────────────────
// These differ from the fast AllRealWire host apps in three deliberate ways:
//   1) ConnectionStrings:DefaultConnection is wired to a real Testcontainers SQL database, so each
//      worker boots its real audit/persistence graph (AddDatabaseServices).
//   2) Nothing in the pipeline is stubbed — the real FileSystemLoader / quality / Tesseract OCR /
//      fusion / classifier / SIRO exporter registered by each worker's Program.cs all run.
//   3) Orion runs the REAL SIARA browser download + discovery against the live simulator (its only
//      change is an isolated journal and disabling the autonomous watch loop so the test drives
//      ingestion explicitly).
// GateOrionApp and GateAthenaApp each start a REAL Kestrel listener on a dynamic loopback port
// (dual-host pattern: TestServer host returned to WAF + separate Kestrel host for real TCP).
// GateReconciliatorApp stays on the plain TestServer (nobody connects inbound to it).
// The production SiaraIngestionHubClient and ReconciliationHubClient connect over real TCP so
// SignalR keepalive runs and multi-minute idle windows cannot silently drop cross-process events.

/// <summary>
/// Orion Downloader host for the max-fidelity gate: real SIARA browser download + discovery against the
/// live simulator, real SQL audit. The autonomous watch loop is disabled so the test drives ingestion.
/// Exposes a real Kestrel listener on a dynamic loopback port via <see cref="HostedBaseAddress"/>.
/// </summary>
internal sealed class GateOrionApp : WebApplicationFactory<global::Prisma.Orion.Worker.Program>
{
    private readonly string _jwtSecret;
    private readonly string _sharedStorageDir;
    private readonly string _connectionString;
    private readonly string _storageState;
    private readonly string _journalPath;
    private IHost? _host;

    /// <summary>
    /// The base address of the real Kestrel listener. Populated after <see cref="WebApplicationFactory{T}.Services"/>
    /// is first accessed (which triggers <see cref="CreateHost"/>). Use this to build URLs for downstream clients
    /// that must connect over real TCP.
    /// </summary>
    internal Uri? HostedBaseAddress { get; private set; }

    internal GateOrionApp(
        string jwtSecret, string sharedStorageDir, string connectionString, string storageState, string journalPath)
    {
        _jwtSecret = jwtSecret;
        _sharedStorageDir = sharedStorageDir;
        _connectionString = connectionString;
        _storageState = storageState;
        _journalPath = journalPath;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["ProcessIdentity:JwtSecret"]      = _jwtSecret,
                ["ProcessIdentity:JwtIssuer"]      = "prisma-pipeline",
                ["ProcessIdentity:JwtAudience"]    = "prisma-pipeline",
                ["ProcessIdentity:TokenLifetime"]  = "01:00:00",
                ["ProcessIdentity:Clearance"]      = "Download",
                ["Siara:Actor:ActorId"]            = "orion-downloader-gate",
                ["Siara:Actor:DisplayName"]        = "Orion Downloader (Gate)",
                ["Storage:BasePath"]               = _sharedStorageDir,
                // Real credential-free SIARA auth against the simulator.
                ["Siara:AuthMode"]                 = "SessionPassthrough",
                ["Siara:Passthrough:Transport"]    = "StorageState",
                ["Siara:Passthrough:StorageStateRef"] = _storageState,
                ["Siara:Passthrough:DashboardUrl"] = "http://localhost:5001/",
                ["Siara:Passthrough:PostLoginSelector"] = "#arrivalRateSlider",
                ["NavigationTargets:SiaraUrl"]     = "http://localhost:5001/",
                ["BrowserAutomation:Headless"]     = "true",
                ["BrowserAutomation:IgnoreHttpsErrors"] = "true",
                ["BrowserAutomation:PageTimeoutMs"] = "60000",
                // Mitigate the shared-storage write/read race the same way production does.
                ["Ingestion:PostWriteFlushDelayMs"] = "250",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Isolated journal per run: the simulator serves the same cases every run, so a persisted
            // journal.txt in the worker cwd would dedupe the case and suppress the event. Keep the REAL
            // FileIngestionJournal — just point it at a per-run temp path.
            services.RemoveAll<IIngestionJournal>();
            services.AddSingleton<IIngestionJournal>(sp =>
                new FileIngestionJournal(_journalPath, sp.GetRequiredService<ILogger<FileIngestionJournal>>()));

            // Disable the autonomous watch loop (OrionWorkerService) — the gate drives discovery +
            // ingestion explicitly so the run is deterministic. Everything else (real downloader, real
            // discovery source, real SignalR broadcaster) stays.
            var watchLoopHosted = services
                .Where(s => s.ImplementationType?.Name == "OrionWorkerService")
                .ToList();
            foreach (var descriptor in watchLoopHosted)
            {
                services.Remove(descriptor);
            }
        });
    }

    /// <inheritdoc/>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Build the in-memory TestServer host first — WAF requires the returned host to expose a TestServer.
        var testHost = builder.Build();

        // Reconfigure the same deferred builder to add a real Kestrel listener on a dynamic loopback port,
        // then build and start that second host. This is the dual-host pattern from PrismaWebApplicationFactory.
        builder.ConfigureWebHost(webHost =>
            webHost.UseKestrel(o => o.Listen(System.Net.IPAddress.Loopback, 0)));

        try
        {
            _host = builder.Build();
            _host.Start();

            var addresses = _host.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()?.Addresses;
            var firstAddress = addresses?.FirstOrDefault();
            if (firstAddress is not null)
            {
                // Ensure address ends with '/' so Uri concatenation works correctly.
                var normalized = firstAddress.TrimEnd('/') + '/';
                HostedBaseAddress = new Uri(normalized);
            }

            testHost.Start();
            return testHost;
        }
        catch
        {
            testHost.Dispose();
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _host?.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// Athena Extractor host for the max-fidelity gate: the full real pipeline (FileSystemLoader →
/// PolynomialImageQualityAnalyzer → TesseractOcrExecutor → FusionExpedienteService) plus real SQL audit.
/// Nothing is stubbed. The production <see cref="SiaraIngestionHubClient"/> connects to the Orion hub over
/// a real TCP loopback connection (no <c>HttpMessageHandlerFactory</c> override — that is the point).
/// Exposes a real Kestrel listener on a dynamic loopback port via <see cref="HostedBaseAddress"/>.
/// </summary>
internal sealed class GateAthenaApp : WebApplicationFactory<global::Prisma.Athena.Worker.Program>
{
    private readonly string _jwtSecret;
    private readonly string _sharedStorageDir;
    private readonly string _connectionString;
    private readonly GateOrionApp _orionApp;
    private readonly bool _runExtractionPipeline;
    private IHost? _host;

    /// <summary>
    /// The base address of the real Kestrel listener. Populated after <see cref="WebApplicationFactory{T}.Services"/>
    /// is first accessed (which triggers <see cref="CreateHost"/>). Use this to build URLs for downstream clients
    /// that must connect over real TCP.
    /// </summary>
    internal Uri? HostedBaseAddress { get; private set; }

    internal GateAthenaApp(
        string jwtSecret, string sharedStorageDir, string connectionString, GateOrionApp orionApp,
        bool runExtractionPipeline = true)
    {
        _jwtSecret = jwtSecret;
        _sharedStorageDir = sharedStorageDir;
        _connectionString = connectionString;
        _orionApp = orionApp;
        _runExtractionPipeline = runExtractionPipeline;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // _orionApp.Services was accessed before this factory was built (BuildThreeHostsWithDb order:
            // Orion.Services → Athena.Services), so Orion's Kestrel host is already started and
            // HostedBaseAddress is populated by the time ConfigureAppConfiguration runs here.
            if (_orionApp.HostedBaseAddress is null)
            {
                throw new InvalidOperationException(
                    "GateOrionApp.HostedBaseAddress is null when building GateAthenaApp. " +
                    "Ensure _orionApp.Services is accessed (triggering CreateHost) before building GateAthenaApp.");
            }

            var ingestionHubUrl = new Uri(_orionApp.HostedBaseAddress, "hubs/ingestion").ToString();

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["ProcessIdentity:JwtSecret"]     = _jwtSecret,
                ["ProcessIdentity:JwtIssuer"]     = "prisma-pipeline",
                ["ProcessIdentity:JwtAudience"]   = "prisma-pipeline",
                ["ProcessIdentity:TokenLifetime"] = "01:00:00",
                ["ProcessIdentity:Clearance"]     = "Extract",
                ["Siara:Actor:ActorId"]           = "athena-extractor-gate",
                ["Siara:Actor:DisplayName"]       = "Athena Extractor (Gate)",
                ["Storage:BasePath"]              = _sharedStorageDir,
                // Real TCP URL — SiaraIngestionHubClient uses its default handler (no factory override).
                ["Ingestion:HubUrl"]              = ingestionHubUrl,
                ["Ingestion:ReconnectDelay"]      = "00:00:00.200",
            });
        });

        builder.ConfigureServices(services =>
        {
            // NO HttpMessageHandlerFactory override — the production SiaraIngestionHubClient connects to
            // Orion's real Kestrel listener over TCP so SignalR keepalive runs normally.

            // Optionally disable the OCR extraction pipeline (AthenaWorkerService drives
            // ExtractionPipelineService.StartAsync) by setting runExtractionPipeline: false. This flag is now
            // reserved for tests that want to isolate the ingestion/handoff contract without OCR; the original
            // rationale (running native Tesseract a second time in the same test process deadlocked) no longer
            // applies — PRISMA-E2-S4 (2026-06-20) fixed the second-init deadlock by lazy-initializing and
            // reusing a single TesseractEngine for the singleton executor's lifetime.
            if (!_runExtractionPipeline)
            {
                var pipelineHosted = services
                    .Where(s => s.ImplementationType?.Name == "AthenaWorkerService")
                    .ToList();
                foreach (var descriptor in pipelineHosted)
                {
                    services.Remove(descriptor);
                }
            }
        });
    }

    /// <inheritdoc/>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Build the in-memory TestServer host first — WAF requires the returned host to expose a TestServer.
        var testHost = builder.Build();

        // Add a real Kestrel listener on a dynamic loopback port so the downstream Reconciliator's
        // ReconciliationHubClient can connect over real TCP (same dual-host pattern as GateOrionApp).
        builder.ConfigureWebHost(webHost =>
            webHost.UseKestrel(o => o.Listen(System.Net.IPAddress.Loopback, 0)));

        try
        {
            _host = builder.Build();
            _host.Start();

            var addresses = _host.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()?.Addresses;
            var firstAddress = addresses?.FirstOrDefault();
            if (firstAddress is not null)
            {
                var normalized = firstAddress.TrimEnd('/') + '/';
                HostedBaseAddress = new Uri(normalized);
            }

            testHost.Start();
            return testHost;
        }
        catch
        {
            testHost.Dispose();
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _host?.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// Reconciliator host for the max-fidelity gate: real FileClassifierService + real SiroXmlExporter +
/// real SQL audit. Exposes an observable <see cref="EventPublisher"/> so the test can await the terminal
/// export/completion events. Stays on the plain TestServer (no inbound SignalR connections to the Reconciliator).
/// </summary>
internal sealed class GateReconciliatorApp : WebApplicationFactory<global::Prisma.Reconciliator.Worker.Program>
{
    private readonly string _jwtSecret;
    private readonly string _sharedStorageDir;
    private readonly string _connectionString;
    private readonly GateAthenaApp _athenaApp;

    internal readonly EventPublisher ReconciliatorEventPublisher = new(NullLogger<EventPublisher>.Instance);

    internal GateReconciliatorApp(
        string jwtSecret, string sharedStorageDir, string connectionString, GateAthenaApp athenaApp)
    {
        _jwtSecret = jwtSecret;
        _sharedStorageDir = sharedStorageDir;
        _connectionString = connectionString;
        _athenaApp = athenaApp;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // _athenaApp.Services was accessed before this factory was built (BuildThreeHostsWithDb order:
            // Athena.Services → Reconciliator build), so Athena's Kestrel host is started and
            // HostedBaseAddress is populated by the time ConfigureAppConfiguration runs here.
            if (_athenaApp.HostedBaseAddress is null)
            {
                throw new InvalidOperationException(
                    "GateAthenaApp.HostedBaseAddress is null when building GateReconciliatorApp. " +
                    "Ensure _athenaApp.Services is accessed (triggering CreateHost) before building GateReconciliatorApp.");
            }

            var reconciliationHubUrl = new Uri(_athenaApp.HostedBaseAddress, "hubs/reconciliation").ToString();

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["ProcessIdentity:JwtSecret"]       = _jwtSecret,
                ["ProcessIdentity:JwtIssuer"]       = "prisma-pipeline",
                ["ProcessIdentity:JwtAudience"]     = "prisma-pipeline",
                ["ProcessIdentity:TokenLifetime"]   = "01:00:00",
                ["ProcessIdentity:Clearance"]       = "Reconcile",
                ["Siara:Actor:ActorId"]             = "reconciliator-gate",
                ["Siara:Actor:DisplayName"]         = "Reconciliator (Gate)",
                ["Storage:BasePath"]                = _sharedStorageDir,
                // Real TCP URL — ReconciliationHubClient uses its default handler (no factory override).
                ["Reconciliation:HubUrl"]           = reconciliationHubUrl,
                ["Reconciliation:ReconnectDelay"]   = "00:00:00.200",
            });
        });

        builder.ConfigureServices(services =>
        {
            // NO HttpMessageHandlerFactory override — the production ReconciliationHubClient connects to
            // Athena's real Kestrel listener over TCP so SignalR keepalive runs normally.

            // Replace IEventPublisher with our observable instance so the test can subscribe.
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher>(ReconciliatorEventPublisher);
        });
    }
}
