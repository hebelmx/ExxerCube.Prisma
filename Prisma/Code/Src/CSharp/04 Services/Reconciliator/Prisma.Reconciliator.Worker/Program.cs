using Serilog;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.Database.Startup;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;
using ExxerCube.Prisma.Infrastructure.Classification;
using ExxerCube.Prisma.Infrastructure.Database.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.Events;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.Export.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Prisma.Athena.Processing;
using Prisma.Athena.Processing.Reconciliation;
using Prisma.Reconciliator.HealthChecks;
using Prisma.Reconciliator.Worker;

var builder = WebApplication.CreateBuilder(args);

// RV-3: Pre-set the SEQ_URL env var before building Log.Logger so the appsettings.json
// %SEQ_URL% token resolves via Serilog's environment-variable expansion. Default to
// localhost when unset (prevents a boot-time UriFormatException from the Seq sink).
Environment.SetEnvironmentVariable(
    "SEQ_URL",
    Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://localhost:5341");

// Configure Serilog from appsettings (Console/Seq). The SQL audit-log sink is added in code
// here, and ONLY when a real connection string is present: the Serilog.Sinks.MSSqlServer v8
// sink constructs eagerly and throws ArgumentNullException on a null/empty connectionString,
// so it cannot be a static appsettings sink that "skips when blank" (it does not — proven by
// the max-fidelity gate). Binding to DefaultConnection keeps the structured-log table in the
// same database as the EF audit ledger (G-S2).
var loggerConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext();

var serilogSqlConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(serilogSqlConnection)
    && !serilogSqlConnection.StartsWith("DEV-PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
{
    loggerConfiguration.WriteTo.MSSqlServer(
        connectionString: serilogSqlConnection,
        sinkOptions: new Serilog.Sinks.MSSqlServer.MSSqlServerSinkOptions
        {
            TableName = "SerilogLogs",
            SchemaName = "dbo",
            AutoCreateSqlTable = true,
        },
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning);
}

Log.Logger = loggerConfiguration.CreateLogger();
builder.Host.UseSerilog();

// Audit persistence (MVP-PATH 1.6 A6): wire AddDatabaseServices when a real connection string is
// provided. Skip gracefully when blank so the worker boots in dev/test without a DB.
var auditConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(auditConnectionString)
    && !auditConnectionString.StartsWith("DEV-PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDatabaseServices(auditConnectionString, builder.Configuration);
}
else
{
    var startupLogger = LoggerFactory.Create(l => l.AddConsole()).CreateLogger("Reconciliator.Worker.Startup");
    startupLogger.LogWarning(
        "Audit persistence DISABLED for Reconciliator Worker: ConnectionStrings:DefaultConnection is blank or placeholder. " +
        "Set ConnectionStrings__DefaultConnection via environment variable or user-secrets for production.");
}

// Per-process JWT clearance token service (MVP-PATH 1.5, A5): the Reconciliator does not broadcast
// outbound (it is the terminal stage), but it will validate incoming clearance tokens in its forwarder
// (A5 DoD, task 1.5c). Also registers ISiaraActorIdentityProvider for the Reconciliator actor identity.
builder.Services.AddProcessIdentity(builder.Configuration);

// The Reconciliator actor of the 3-process split (MVP-PATH 1.4, ADR-011). It is a SignalR CLIENT of the Athena
// Extractor's /hubs/reconciliation: it receives ExtractionCompletedEvent, loads the fused expediente from the
// shared volume, and runs Classification → Export.
builder.Services.AddSingleton<IEventPublisher, EventPublisher>();

// Cross-process reconciliation subscriber: connect to the Athena reconciliation hub and republish each
// ExtractionCompletedEvent onto the local stream the pipeline subscribes to.
builder.Services.Configure<ReconciliationClientOptions>(
    builder.Configuration.GetSection(ReconciliationClientOptions.SectionName));
builder.Services.AddSingleton<IClearanceReplayGuard, InMemoryClearanceReplayGuard>();
builder.Services.AddSingleton<ReconciliationEventForwarder>();
builder.Services.AddHostedService<ReconciliationHubClient>();

// Shared document storage (ADR-011): the Reconciliator mounts the same volume the Extractor wrote the fused
// expediente handoff to, possibly at a different absolute path, and resolves the event's storage-relative path
// against its own configured base before loading the handoff artifact.
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddSingleton<IStoragePathResolver, SharedStoragePathResolver>();
builder.Services.AddSingleton<IExpedienteHandoffStore, FileSystemExpedienteHandoffStore>();

// Reconciliator pipeline services. Classification is always wired. Export uses the SIRO XML path
// (MVP-PATH #8, ADR-011): AddSiroExportServices registers SiroXmlExporter + CompositeResponseExporter
// (IResponseExporter) — no IMetadataExtractor / IPdfRequirementSummarizer dependency, so the host DI
// graph validates cleanly.
//
// IMPORTANT: pass ServiceLifetime.Singleton. ReconciliationOrchestrator is a Singleton and captures
// IResponseExporter at construction time from the root provider. Scoped services cannot be resolved
// from the root provider (ValidateScopes rejects it). The SIRO export types are stateless so Singleton
// is safe. Do NOT call AddExportServices (also registers PdfRequirementSummarizerService → IMetadataExtractor,
// not available here) and do NOT call AddAdaptiveExportServices (overrides IResponseExporter with
// AdaptiveResponseExporterAdapter which emits <Export>, NOT <SiroResponse>).
builder.Services.AddSiroExportServices(builder.Configuration, ServiceLifetime.Singleton);

// "Datos Carga de Oficio" xlsx layout generator (FR-A, Item #7).
// Singleton lifetime — stateless, safe to share. No TemplateDbContext required: falls back to built-in
// DatosCargaOficioTemplate.Default when no ITemplateRepository override is wired.
builder.Services.AddDatosCargaOficioExportServices(ServiceLifetime.Singleton);

builder.Services.AddSingleton<IFileClassifier, FileClassifierService>();

// G-C2b: register the approval-triggered re-export handler as a singleton.
// StartAsync is called after app.Build() below so the IEventPublisher Rx stream is
// already alive when the subscription is attached.
builder.Services.AddSingleton<ReviewApprovalExportHandler>();
builder.Services.AddSingleton<ReconciliationOrchestrator>(sp => new ReconciliationOrchestrator(
    sp.GetRequiredService<IEventPublisher>(),
    sp.GetRequiredService<ILogger<ReconciliationOrchestrator>>(),
    classifier: sp.GetRequiredService<IFileClassifier>(),
    exporter: sp.GetService<IResponseExporter>(),
    reviewCaseScopeFactory: sp.GetService<IServiceScopeFactory>(),
    datosCargaGenerator: sp.GetService<IDatosCargaOficioLayoutGenerator>(),
    storagePathResolver: sp.GetService<IStoragePathResolver>()));

// Per-process audit (MVP-PATH 1.6 A6): ReconciliationPipelineService is singleton; IAuditLogger is scoped.
// The service resolves IAuditLogger per audit call via IServiceScopeFactory (no captive dependency).
builder.Services.AddSingleton<ReconciliationPipelineService>(sp =>
{
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    var actorIdentityProvider = sp.GetService<ISiaraActorIdentityProvider>();
    var processOptions = sp.GetService<IOptions<ProcessIdentityOptions>>();
    var clearance = processOptions?.Value.Clearance ?? ProcessClearance.Reconcile;

    return new ReconciliationPipelineService(
        sp.GetRequiredService<IEventPublisher>(),
        sp.GetRequiredService<ReconciliationOrchestrator>(),
        sp.GetRequiredService<IExpedienteHandoffStore>(),
        sp.GetRequiredService<ILogger<ReconciliationPipelineService>>(),
        scopeFactory: scopeFactory,
        actorIdentityProvider: actorIdentityProvider,
        processClearance: clearance);
});
builder.Services.AddHostedService<ReconciliatorWorkerService>();

// Readiness (MVP-PATH 4.2 / E1-S4): the Reconciliation pipeline is the component this worker actually starts,
// so it is the truthful readiness signal. Resolve the same singleton instance behind IReadinessProbe.
builder.Services.AddSingleton<IReadinessProbe>(sp => sp.GetRequiredService<ReconciliationPipelineService>());

// Register health check service backed by the real IReadinessProbe.
builder.Services.AddSingleton<IHealthCheckService, ReconciliatorHealthCheckService>();

var app = builder.Build();

// G-C2b: start the approval-triggered re-export handler.  Must be called after Build() so the
// singleton IEventPublisher (EventPublisher, Subject<DomainEvent>) is resolved from the root
// provider and the Rx stream is alive before the subscription is attached.
app.Services.GetRequiredService<ReviewApprovalExportHandler>()
    .StartAsync(cancellationToken: default);

// Health endpoints — mirror the Athena/Orion idiom (Orion Program.cs:225-244).
// /health     → overall (Healthy 200 / Degraded 503)
// /health/live → liveness only (always 200 while process is alive)
// /health/ready → readiness (Healthy 200 / Unhealthy 503); this is the probe that gates traffic.
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

// ── K8s init-container migration path ─────────────────────────────────────
// Run `--migrate-only` to apply EF Core migrations and exit without starting
// the normal host run loop. Intended for use as a K8s init-container:
//   command: ["./ExxerCube.Prisma.Reconciliator.Worker", "--migrate-only"]
if (args.Contains("--migrate-only"))
{
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    var exitCode = await PrismaDbMigrationRunner.RunMigrationsAsync(app, cts.Token);
    Environment.ExitCode = exitCode;
    return;
}
// ──────────────────────────────────────────────────────────────────────────

await app.RunAsync();

namespace Prisma.Reconciliator.Worker
{
    /// <summary>
    /// Program class for the Reconciliator Worker (made public for WebApplicationFactory testing).
    /// </summary>
    public partial class Program { }
}
