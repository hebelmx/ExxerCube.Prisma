using System.Text;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Services.Manifest;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.FileSystem;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.NavigationTargets;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;
using ExxerCube.Prisma.Infrastructure.Database.DependencyInjection;
using ExxerCube.Prisma.Domain.Serialization;
using IndFusion.Ember.Abstractions.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Prisma.Orion.HealthChecks;
using Prisma.Orion.Ingestion;
using Prisma.Orion.Worker;
using Prisma.Orion.Worker.Ingestion;

var builder = WebApplication.CreateBuilder(args);

// Audit persistence (MVP-PATH 1.6 A6): wire AddDatabaseServices when a real connection string is
// provided. Skip gracefully when blank so the worker boots in dev/test without a DB.
// The Downloader is lean (no local Rx event stream / IEventPublisher), so opt out of the
// EventPersistenceWorker subscriber — it would fail to construct and block host startup.
var auditConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(auditConnectionString)
    && !auditConnectionString.StartsWith("DEV-PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDatabaseServices(auditConnectionString, builder.Configuration, registerEventPersistence: false);
}
else
{
    var startupLogger = LoggerFactory.Create(l => l.AddConsole()).CreateLogger("Orion.Worker.Startup");
    startupLogger.LogWarning(
        "Audit persistence DISABLED for Orion Worker: ConnectionStrings:DefaultConnection is blank or placeholder. " +
        "Set ConnectionStrings__DefaultConnection via environment variable or user-secrets for production.");
}

// Register orchestrator dependencies
builder.Services.AddSingleton<IIngestionJournal>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<FileIngestionJournal>>();
    var journalPath = Path.Combine(Directory.GetCurrentDirectory(), "journal.txt");
    return new FileIngestionJournal(journalPath, logger);
});
// Real SIARA ingestion (MVP-PATH 1.1): the browser-automation scraper + the credential-free SIARA auth
// seam (ADR-010), adapted into the IDocumentDownloader port. Replaces StubDocumentDownloader.
builder.Services.AddBrowserAutomationServices(options =>
    builder.Configuration.GetSection("BrowserAutomation").Bind(options));
builder.Services.Configure<NavigationTargetOptions>(options =>
    builder.Configuration.GetSection("NavigationTargets").Bind(options));
builder.Services.AddSiaraAuthentication(builder.Configuration);

// Per-process JWT clearance token service (MVP-PATH 1.5, A5): mints tokens the broadcaster stamps on
// events; AddSiaraAuthentication already registered ISiaraActorIdentityProvider so TryAdd is a no-op.
builder.Services.AddProcessIdentity(builder.Configuration);

// The real downloader is scoped: it rides a scoped Playwright browser and SIARA session. The watch loop
// creates a fresh scope per document (SiaraWatchLoop), so the singleton host never captures it.
builder.Services.AddScoped<IDocumentDownloader, SiaraDocumentDownloader>();

// The discovery source (MVP-PATH 1.2 — the "list" half) is scoped for the same reason; the watch loop
// keeps one long-lived discovery scope so the SIARA session stays warm across cycles.
builder.Services.AddScoped<ISiaraDocumentSource, SiaraDocumentSource>();

// Connection-level hub auth (follow-up to MVP-PATH 1.5): the ingestion hub only accepts clients that
// present a valid JWT clearance token with ProcessClearance.Extract. This is the standard SignalR JWT
// pattern — WebSocket upgrades cannot carry Authorization headers, so the token is read from the
// access_token query-string parameter. The same ProcessIdentityOptions.JwtSecret that mints per-message
// clearance tokens also validates the connection-level token (same signing key, shared across processes).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Read the signing key from ProcessIdentityOptions after the DI container is built. We use a
        // post-build options callback so the same IOptions<ProcessIdentityOptions> singleton is the
        // single source of truth.
        var processIdentityOptions = builder.Configuration
            .GetSection(ProcessIdentityOptions.SectionName)
            .Get<ProcessIdentityOptions>() ?? new ProcessIdentityOptions();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(processIdentityOptions.JwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = processIdentityOptions.JwtIssuer,
            ValidateAudience = true,
            ValidAudience = processIdentityOptions.JwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // Standard SignalR JWT pattern: bearer tokens for WebSocket/SSE arrive in the access_token
        // query-string parameter rather than the Authorization header. This event copies it to the
        // request context so the bearer handler can validate it normally.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/ingestion"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
    });

// Authorization policy: the ingestion hub requires the connecting client to carry ProcessClearance.Extract.
// A token with a different clearance value is rejected at connection time (connection refused, not just
// silently dropped). This is additive to the per-message clearance check in IngestionEventForwarder (1.5c).
builder.Services.AddAuthorization(authOptions =>
    authOptions.AddPolicy(HubAuthPolicies.RequireExtractClearance, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("clearance", ProcessClearance.Extract.ToString())));

// The real IndFusion.Ember transport (MVP-PATH 1.3): the Orion Downloader actor hosts a SignalR hub and
// broadcasts DocumentDownloadedEvent to the downstream Extractor (Athena) — the first cross-process edge of
// the Three-Actors split (ADR-009/ADR-011). Replaces StubExxerHub. The broadcaster sends via IHubContext
// (a DI-resolved hub instance has a null Clients and cannot send).
// Register the SmartEnum (EnumModel) JSON converter on the hub protocol: domain events on the wire carry
// SmartEnums (e.g. CaseFileReference.Format) that default System.Text.Json serializes as objects and cannot
// reconstruct — silently collapsing every Format to its zero-value singleton and hiding the XML/DOCX
// companions from Stage-3 fusion (max-fidelity gate #5 diagnosis, 2026-06-14). Both ends of the edge must
// agree, so the Athena ingestion client registers the same converter.
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new EnumModelJsonConverterFactory()));
builder.Services.AddSingleton<IExxerHub<DocumentDownloadedEvent>, SignalRIngestionBroadcaster>();

// The orchestrator is scoped because it depends on the scoped downloader; the worker resolves it inside
// a per-work scope and the health/dashboard endpoints resolve it from the per-request scope.
builder.Services.AddScoped<IngestionOrchestrator>(sp =>
{
    var journal = sp.GetRequiredService<IIngestionJournal>();
    var downloader = sp.GetRequiredService<IDocumentDownloader>();
    var eventHub = sp.GetRequiredService<IExxerHub<DocumentDownloadedEvent>>();
    var logger = sp.GetRequiredService<ILogger<IngestionOrchestrator>>();

    // Shared document storage base (ADR-011): config-driven so the Downloader and Extractor can point at
    // the same shared volume. When blank, the orchestrator defaults to ./storage (single-box dev).
    var storageBasePath = builder.Configuration["Storage:BasePath"];

    // Per-process audit (MVP-PATH 1.6 A6): wire IAuditLogger (via IServiceScopeFactory to avoid captive
    // dependency — IAuditLogger is scoped) + ISiaraActorIdentityProvider + Download clearance.
    // Both are optional; when AddDatabaseServices was skipped (dev/test without DB), scopeFactory is
    // present but IAuditLogger will not be registered — EmitAuditAsync guards with a try/catch (fail-open).
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    var actorIdentityProvider = sp.GetService<ISiaraActorIdentityProvider>();
    var processOptions = sp.GetService<IOptions<ProcessIdentityOptions>>();
    var clearance = processOptions?.Value.Clearance ?? ProcessClearance.Download;

    // Post-write flush delay (MVP-PATH 2.1, owner I/O-race mitigation): configurable via
    // Ingestion:PostWriteFlushDelayMs; defaults to 250 ms when absent.
    TimeSpan? postWriteFlushDelay = null;
    var delayMsStr = builder.Configuration["Ingestion:PostWriteFlushDelayMs"];
    if (int.TryParse(delayMsStr, out var delayMs))
    {
        postWriteFlushDelay = TimeSpan.FromMilliseconds(delayMs);
    }

    // TimeProvider from DI if registered (e.g. FakeTimeProvider in tests), else system default.
    var timeProvider = sp.GetService<TimeProvider>();

    return new IngestionOrchestrator(
        journal, downloader, eventHub, logger, storageBasePath,
        scopeFactory: scopeFactory,
        actorIdentityProvider: actorIdentityProvider,
        processClearance: clearance,
        timeProvider: timeProvider,
        postWriteFlushDelay: postWriteFlushDelay);
});

// The SIARA watch loop (MVP-PATH 1.2): a singleton poll/watcher that owns the DI scopes (one warm
// discovery scope + a fresh scope per document). Driven by the hosted worker.
builder.Services.Configure<WatchLoopOptions>(
    builder.Configuration.GetSection(WatchLoopOptions.SectionName));

// Per-cycle manifest reconciliation (Item B #8 + F #12): opt-in via ExpectedManifest:Enabled=true.
// When disabled (default), the watch loop behaves exactly as before.
builder.Services.Configure<ExpectedManifestOptions>(
    builder.Configuration.GetSection(ExpectedManifestOptions.SectionName));
builder.Services.AddSingleton<IExpectedManifestProvider, FileExpectedManifestProvider>();
builder.Services.AddSingleton<IManifestReconciler, ManifestReconciliationService>();

// Shared document storage (ADR-011): the Downloader mounts its own storage base and emits only the
// relative path on the event. IStoragePathResolver turns that relative path into a locally-loadable
// absolute path for the cycle-report persist step in SiaraWatchLoop (and any downstream consumer).
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddSingleton<IStoragePathResolver, SharedStoragePathResolver>();

builder.Services.AddSingleton<SiaraWatchLoop>();
builder.Services.AddHostedService<OrionWorkerService>();

// Readiness (MVP-PATH 4.2 / E1): the SIARA watch loop is the component this worker actually starts, so it
// is the truthful readiness signal. Resolve the same singleton instance behind IReadinessProbe.
builder.Services.AddSingleton<IReadinessProbe>(sp => sp.GetRequiredService<SiaraWatchLoop>());

// Health + dashboard depend (transitively) on the scoped orchestrator, so they are scoped too; the
// minimal-API endpoints resolve them from the per-request scope.
builder.Services.AddScoped<IHealthCheckService, OrionHealthCheckService>();
builder.Services.AddScoped<IDashboardService, OrionDashboardService>();

var app = builder.Build();

// Hub auth middleware: must appear before MapHub so the JWT bearer scheme can authenticate the
// SignalR upgrade request before SignalR dispatches it to the hub.
app.UseAuthentication();
app.UseAuthorization();

// The ingestion hub (MVP-PATH 1.3): downstream Extractors (Athena) connect here to receive
// DocumentDownloadedEvent over the real Ember transport.
app.MapHub<IngestionHub>("/hubs/ingestion");

// Health endpoints
app.MapGet("/health", async (IHealthCheckService healthCheck, CancellationToken ct) =>
{
    var result = await healthCheck.GetHealthAsync(ct);
    return result.Status == OrchestratorHealthState.Healthy
        ? Results.Ok(new { status = result.Status.ToString(), description = result.Description, data = result.Data })
        : Results.Json(new { status = result.Status.ToString(), description = result.Description, data = result.Data }, statusCode: 503);
});

app.MapGet("/health/live", async (IHealthCheckService healthCheck, CancellationToken ct) =>
{
    var result = await healthCheck.GetLivenessAsync(ct);
    return Results.Ok(new { status = result.Status.ToString(), description = result.Description, data = result.Data });
});

app.MapGet("/health/ready", async (IHealthCheckService healthCheck, CancellationToken ct) =>
{
    var result = await healthCheck.GetReadinessAsync(ct);
    return result.Status == OrchestratorHealthState.Healthy
        ? Results.Ok(new { status = result.Status.ToString(), description = result.Description, data = result.Data })
        : Results.Json(new { status = result.Status.ToString(), description = result.Description, data = result.Data }, statusCode: 503);
});

// Dashboard endpoint
app.MapGet("/dashboard", async (IDashboardService dashboard, CancellationToken ct) =>
{
    var stats = await dashboard.GetStatsAsync(ct);
    return Results.Ok(stats);
});

await app.RunAsync();

namespace Prisma.Orion.Worker
{
    /// <summary>
    /// Program class for Orion Worker (made public for WebApplicationFactory testing).
    /// </summary>
    public partial class Program { }
}