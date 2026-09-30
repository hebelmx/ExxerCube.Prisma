using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Worker.Ingestion;
using Prisma.Orion.Ingestion;

namespace ExxerCube.Prisma.Tests.AllRealWireE2E;

// ── Max-fidelity gate (#5) worker hosts ───────────────────────────────────────────────────────────
// Each worker is booted as a SINGLE real Kestrel host (NOT WebApplicationFactory) so that the
// service provider used to publish events and the Kestrel host that services inbound hub connections
// are THE SAME host. The previous dual-host pattern (WAF in-memory TestServer + a separate Kestrel
// host) introduced split DI lifetimes: broadcasting from _orionApp.Services went to the TestServer
// host's SignalR lifetime manager while Athena's SiaraIngestionHubClient connected to the separate
// Kestrel host's manager — events were never delivered. Single-host eliminates that split.
//
// Connection-string bootstrap: configureEarly runs immediately after WebApplication.CreateBuilder
// so AddInMemoryCollection fires BEFORE any of the inline config reads in Program.BuildApp. The
// old env-var hack (ConnectionStrings__DefaultConnection set at process level) is gone.

/// <summary>
/// Orion Downloader host for the max-fidelity gate: real SIARA browser download + discovery against the
/// live simulator, real SQL audit. The autonomous watch loop is disabled so the test drives ingestion.
/// Single real Kestrel listener on a dynamic loopback port; <see cref="BaseAddress"/> is populated after
/// <see cref="StartAsync"/> returns.
/// </summary>
internal sealed class GateOrionHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    /// <summary>Base address of the real Kestrel listener (trailing slash included).</summary>
    public Uri BaseAddress { get; }

    /// <summary>The single real host's service provider.</summary>
    public IServiceProvider Services => _app.Services;

    private GateOrionHost(WebApplication app, Uri baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// Builds and starts the Orion worker as a single real Kestrel host wired to the gate's SQL database.
    /// </summary>
    public static async Task<GateOrionHost> StartAsync(
        string jwtSecret,
        string sharedStorageDir,
        string connectionString,
        string storageState,
        string journalPath,
        CancellationToken ct)
    {
        var app = global::Prisma.Orion.Worker.Program.BuildApp(
            args: Array.Empty<string>(),
            configureEarly: builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"]      = connectionString,
                    ["ProcessIdentity:JwtSecret"]                = jwtSecret,
                    ["ProcessIdentity:JwtIssuer"]                = "prisma-pipeline",
                    ["ProcessIdentity:JwtAudience"]              = "prisma-pipeline",
                    ["ProcessIdentity:TokenLifetime"]            = "01:00:00",
                    ["ProcessIdentity:Clearance"]                = "Download",
                    ["Siara:Actor:ActorId"]                      = "orion-downloader-gate",
                    ["Siara:Actor:DisplayName"]                  = "Orion Downloader (Gate)",
                    ["Storage:BasePath"]                         = sharedStorageDir,
                    ["Siara:AuthMode"]                           = "SessionPassthrough",
                    ["Siara:Passthrough:Transport"]              = "StorageState",
                    ["Siara:Passthrough:StorageStateRef"]        = storageState,
                    ["Siara:Passthrough:DashboardUrl"]           = "http://localhost:5001/",
                    ["Siara:Passthrough:PostLoginSelector"]      = "#arrivalRateSlider",
                    ["NavigationTargets:SiaraUrl"]               = "http://localhost:5001/",
                    ["BrowserAutomation:Headless"]               = "true",
                    ["BrowserAutomation:IgnoreHttpsErrors"]      = "true",
                    ["BrowserAutomation:PageTimeoutMs"]          = "60000",
                    ["Ingestion:PostWriteFlushDelayMs"]          = "250",
                });
            },
            configureServicesLate: services =>
            {
                // Per-run isolated journal: the simulator serves the same cases every run, so a
                // persisted journal.txt would dedupe the case and suppress the event.
                services.RemoveAll<IIngestionJournal>();
                services.AddSingleton<IIngestionJournal>(sp =>
                    new FileIngestionJournal(
                        journalPath,
                        sp.GetRequiredService<ILogger<FileIngestionJournal>>()));

                // Disable the autonomous watch loop — the gate drives discovery + ingestion
                // explicitly so the run is deterministic. Everything else (real downloader,
                // real discovery source, real SignalR broadcaster) stays.
                foreach (var d in services
                    .Where(s => s.ImplementationType?.Name == "OrionWorkerService")
                    .ToList())
                {
                    services.Remove(d);
                }
            });

        await app.StartAsync(ct).ConfigureAwait(false);

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();

        return new GateOrionHost(app, new Uri(address.TrimEnd('/') + "/"));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() =>
        await _app.DisposeAsync().ConfigureAwait(false);
}

/// <summary>
/// Athena Extractor host for the max-fidelity gate: the full real pipeline (FileSystemLoader →
/// PolynomialImageQualityAnalyzer → TesseractOcrExecutor → FusionExpedienteService) plus real SQL
/// audit. The production <c>SiaraIngestionHubClient</c> connects to Orion over real TCP — no
/// <c>HttpMessageHandlerFactory</c> override; that is the point of using a single Kestrel host.
/// <see cref="BaseAddress"/> exposes the reconciliation hub for the downstream Reconciliator.
/// </summary>
internal sealed class GateAthenaHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    /// <summary>Base address of the real Kestrel listener (trailing slash included).</summary>
    public Uri BaseAddress { get; }

    /// <summary>The single real host's service provider.</summary>
    public IServiceProvider Services => _app.Services;

    private GateAthenaHost(WebApplication app, Uri baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// Builds and starts the Athena worker as a single real Kestrel host wired to the gate's SQL
    /// database and Orion's real hub URL.
    /// </summary>
    public static async Task<GateAthenaHost> StartAsync(
        string jwtSecret,
        string sharedStorageDir,
        string connectionString,
        Uri orionBaseAddress,
        bool runExtractionPipeline,
        CancellationToken ct)
    {
        var ingestionHubUrl = new Uri(orionBaseAddress, "hubs/ingestion").ToString();

        var app = global::Prisma.Athena.Worker.Program.BuildApp(
            args: Array.Empty<string>(),
            configureEarly: builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["ProcessIdentity:JwtSecret"]           = jwtSecret,
                    ["ProcessIdentity:JwtIssuer"]           = "prisma-pipeline",
                    ["ProcessIdentity:JwtAudience"]         = "prisma-pipeline",
                    ["ProcessIdentity:TokenLifetime"]       = "01:00:00",
                    ["ProcessIdentity:Clearance"]           = "Extract",
                    ["Siara:Actor:ActorId"]                 = "athena-extractor-gate",
                    ["Siara:Actor:DisplayName"]             = "Athena Extractor (Gate)",
                    ["Storage:BasePath"]                    = sharedStorageDir,
                    // Real TCP URL — SiaraIngestionHubClient uses its default handler (no factory override).
                    ["Ingestion:HubUrl"]                    = ingestionHubUrl,
                    ["Ingestion:ReconnectDelay"]            = "00:00:00.200",
                });
            },
            configureServicesLate: services =>
            {
                // NO HttpMessageHandlerFactory override: the production SiaraIngestionHubClient
                // connects to Orion's real Kestrel listener over TCP so SignalR keepalive runs.

                if (!runExtractionPipeline)
                {
                    // Optionally disable the OCR extraction pipeline. Reserved for tests that
                    // want to isolate the ingestion/handoff contract without OCR.
                    foreach (var d in services
                        .Where(s => s.ImplementationType?.Name == "AthenaWorkerService")
                        .ToList())
                    {
                        services.Remove(d);
                    }
                }
            });

        await app.StartAsync(ct).ConfigureAwait(false);

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();

        return new GateAthenaHost(app, new Uri(address.TrimEnd('/') + "/"));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() =>
        await _app.DisposeAsync().ConfigureAwait(false);
}

/// <summary>
/// Reconciliator host for the max-fidelity gate: real FileClassifierService + real SiroXmlExporter +
/// real SQL audit. Exposes an observable <see cref="ReconciliatorEventPublisher"/> so the test can
/// await the terminal export/completion events. The production <c>ReconciliationHubClient</c> connects
/// to Athena over real TCP — no <c>HttpMessageHandlerFactory</c> override. A Kestrel listener is still
/// started (for health endpoints + symmetry); nobody connects inbound to it.
/// </summary>
internal sealed class GateReconciliatorHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    /// <summary>Base address of the real Kestrel listener (trailing slash included).</summary>
    public Uri BaseAddress { get; }

    /// <summary>The single real host's service provider.</summary>
    public IServiceProvider Services => _app.Services;

    /// <summary>
    /// Observable event publisher injected into the Reconciliator host so the gate test can
    /// subscribe to terminal pipeline events (<see cref="ExportCompletedEvent"/>,
    /// <see cref="DocumentProcessingCompletedEvent"/>).
    /// </summary>
    public EventPublisher ReconciliatorEventPublisher { get; }

    private GateReconciliatorHost(WebApplication app, Uri baseAddress, EventPublisher publisher)
    {
        _app = app;
        BaseAddress = baseAddress;
        ReconciliatorEventPublisher = publisher;
    }

    /// <summary>
    /// Builds and starts the Reconciliator worker as a single real Kestrel host wired to the gate's
    /// SQL database and Athena's real hub URL.
    /// </summary>
    public static async Task<GateReconciliatorHost> StartAsync(
        string jwtSecret,
        string sharedStorageDir,
        string connectionString,
        Uri athenaBaseAddress,
        CancellationToken ct)
    {
        var reconciliationHubUrl = new Uri(athenaBaseAddress, "hubs/reconciliation").ToString();
        var publisher = new EventPublisher(NullLogger<EventPublisher>.Instance);

        var app = global::Prisma.Reconciliator.Worker.Program.BuildApp(
            args: Array.Empty<string>(),
            configureEarly: builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"]  = connectionString,
                    ["ProcessIdentity:JwtSecret"]            = jwtSecret,
                    ["ProcessIdentity:JwtIssuer"]            = "prisma-pipeline",
                    ["ProcessIdentity:JwtAudience"]          = "prisma-pipeline",
                    ["ProcessIdentity:TokenLifetime"]        = "01:00:00",
                    ["ProcessIdentity:Clearance"]            = "Reconcile",
                    ["Siara:Actor:ActorId"]                  = "reconciliator-gate",
                    ["Siara:Actor:DisplayName"]              = "Reconciliator (Gate)",
                    ["Storage:BasePath"]                     = sharedStorageDir,
                    // Real TCP URL — ReconciliationHubClient uses its default handler (no factory override).
                    ["Reconciliation:HubUrl"]                = reconciliationHubUrl,
                    ["Reconciliation:ReconnectDelay"]        = "00:00:00.200",
                });
            },
            configureServicesLate: services =>
            {
                // NO HttpMessageHandlerFactory override: the production ReconciliationHubClient
                // connects to Athena's real Kestrel listener over TCP so SignalR keepalive runs.

                // Replace IEventPublisher with our observable instance so the test can subscribe.
                services.RemoveAll<IEventPublisher>();
                services.AddSingleton<IEventPublisher>(publisher);
            });

        await app.StartAsync(ct).ConfigureAwait(false);

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();

        return new GateReconciliatorHost(app, new Uri(address.TrimEnd('/') + "/"), publisher);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() =>
        await _app.DisposeAsync().ConfigureAwait(false);
}
