using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
// The SignalR transport is still routed through the in-memory TestServer handler (production hub +
// auth code runs unchanged; no TCP port needed) — the same proven seam as the fast harness.

/// <summary>
/// Orion Downloader host for the max-fidelity gate: real SIARA browser download + discovery against the
/// live simulator, real SQL audit. The autonomous watch loop is disabled so the test drives ingestion.
/// </summary>
internal sealed class GateOrionApp : WebApplicationFactory<global::Prisma.Orion.Worker.Program>
{
    private readonly string _jwtSecret;
    private readonly string _sharedStorageDir;
    private readonly string _connectionString;
    private readonly string _storageState;
    private readonly string _journalPath;

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
}

/// <summary>
/// Athena Extractor host for the max-fidelity gate: the full real pipeline (FileSystemLoader →
/// PolynomialImageQualityAnalyzer → TesseractOcrExecutor → FusionExpedienteService) plus real SQL audit.
/// Nothing is stubbed; only the SignalR ingestion-client transport is routed through Orion's TestServer.
/// </summary>
internal sealed class GateAthenaApp : WebApplicationFactory<global::Prisma.Athena.Worker.Program>
{
    private readonly string _jwtSecret;
    private readonly string _sharedStorageDir;
    private readonly string _connectionString;
    private readonly GateOrionApp _orionApp;

    internal GateAthenaApp(
        string jwtSecret, string sharedStorageDir, string connectionString, GateOrionApp orionApp)
    {
        _jwtSecret = jwtSecret;
        _sharedStorageDir = sharedStorageDir;
        _connectionString = connectionString;
        _orionApp = orionApp;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
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
                ["Ingestion:HubUrl"]              = "http://orion-testserver/hubs/ingestion",
                ["Ingestion:ReconnectDelay"]      = "00:00:00.200",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Route the production SiaraIngestionHubClient through Orion's TestServer handler (in-memory
            // SignalR transport; all auth + hub code still runs).
            services.Configure<IngestionClientOptions>(o =>
                o.HttpMessageHandlerFactory = () => _orionApp.Server.CreateHandler());
        });
    }
}

/// <summary>
/// Reconciliator host for the max-fidelity gate: real FileClassifierService + real SiroXmlExporter +
/// real SQL audit. Exposes an observable <see cref="EventPublisher"/> so the test can await the terminal
/// export/completion events.
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
                ["Reconciliation:HubUrl"]           = "http://athena-testserver/hubs/reconciliation",
                ["Reconciliation:ReconnectDelay"]   = "00:00:00.200",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.Configure<ReconciliationClientOptions>(o =>
                o.HttpMessageHandlerFactory = () => _athenaApp.Server.CreateHandler());

            // Replace IEventPublisher with our observable instance so the test can subscribe.
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher>(ReconciliatorEventPublisher);
        });
    }
}
