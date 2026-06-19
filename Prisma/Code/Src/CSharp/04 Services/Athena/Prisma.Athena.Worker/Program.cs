using System.Text;
using Serilog;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.Database.Startup;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;
using ExxerCube.Prisma.Infrastructure.Database.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ExxerCube.Prisma.Infrastructure.Calendar;
using ExxerCube.Prisma.Infrastructure.Classification;
using ExxerCube.Prisma.Infrastructure.Events;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using ExxerCube.Prisma.Infrastructure.Extraction.Txt;
using ExxerCube.Prisma.Infrastructure.FileSystem;
using ExxerCube.Prisma.Infrastructure.Imaging;
using ExxerCube.Prisma.Domain.Serialization;
using IndFusion.Ember.Abstractions.Hubs;
using Microsoft.Extensions.DependencyInjection;
using Prisma.Athena.HealthChecks;
using Prisma.Athena.Processing;
using Prisma.Athena.Processing.Ingestion;
using Prisma.Athena.Worker;
using Prisma.Athena.Worker.Ingestion;
using Prisma.Athena.Worker.Reconciliation;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog from appsettings (mirrors Web.UI pattern)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();
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
    var startupLogger = LoggerFactory.Create(l => l.AddConsole()).CreateLogger("Athena.Worker.Startup");
    startupLogger.LogWarning(
        "Audit persistence DISABLED for Athena Worker: ConnectionStrings:DefaultConnection is blank or placeholder. " +
        "Set ConnectionStrings__DefaultConnection via environment variable or user-secrets for production.");
}

// Per-process JWT clearance token service (MVP-PATH 1.5, A5): mints tokens that the Reconciliation
// broadcaster stamps on ExtractionCompletedEvent before sending to the Reconciliator process.
// Also registers ISiaraActorIdentityProvider (ConfiguredSiaraActorIdentityProvider) for Athena.
builder.Services.AddProcessIdentity(builder.Configuration);

// Register event infrastructure
builder.Services.AddSingleton<IEventPublisher, EventPublisher>();

// Cross-process ingestion (MVP-PATH 1.3): subscribe to the Orion Downloader actor's SignalR hub and
// republish each DocumentDownloadedEvent onto the local event stream the pipeline subscribes to
// (the first cross-process edge of the Three-Actors split — ADR-009/ADR-011).
builder.Services.Configure<IngestionClientOptions>(
    builder.Configuration.GetSection(IngestionClientOptions.SectionName));

// Shared document storage (ADR-011): the Extractor mounts the same volume the Downloader stored into,
// possibly at a different absolute path, so it resolves the event's storage-relative path against its own
// configured base before the pipeline loads the file.
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddSingleton<IStoragePathResolver, SharedStoragePathResolver>();

builder.Services.AddSingleton<IClearanceReplayGuard, InMemoryClearanceReplayGuard>();
builder.Services.AddSingleton<IngestionEventForwarder>();
builder.Services.AddHostedService<SiaraIngestionHubClient>();

// Register pipeline services (adapters → ports)
// PDF rasterization (PDFtoImage/SkiaSharp): FileSystemLoader renders a .pdf primary to PNG so the quality +
// OCR stages can process it. Without this the loader returns raw PDF bytes and OCR cannot read them.
builder.Services.AddSingleton<IPdfToImageConverter, PdfToImageConverter>();
builder.Services.AddSingleton<IFileLoader, FileSystemLoader>();
builder.Services.AddSingleton<IImageQualityAnalyzer, PolynomialImageQualityAnalyzer>();
builder.Services.AddSingleton<IOcrExecutor, TesseractOcrExecutor>();
// Holiday-aware business-day calculator (Item G #13): inject IBusinessDayCalculator into
// FusionExpediente so FechaEstimadaConclusion accounts for Mexican federal holidays, not
// just weekends. Without this the optional ctor param defaults to null → weekend-only fallback.
builder.Services.AddSingleton<IBusinessDayCalculator, MexicoBusinessDayCalculator>();
builder.Services.AddSingleton<IFusionExpediente>(sp => new FusionExpedienteService(
    sp.GetRequiredService<ILogger<FusionExpedienteService>>(),
    businessDayCalculator: sp.GetRequiredService<IBusinessDayCalculator>()));
builder.Services.AddSingleton<IFileClassifier, FileClassifierService>();
// The Extractor (Athena Worker) does NOT run Stage 5 export — export is handled exclusively by the
// Reconciliator process (3-process split, ADR-011). ProcessingOrchestrator takes IResponseExporter?
// and skips export when null, so we deliberately register nothing here.
// Field extractor that turns Stage 2 OCR text into an Expediente feeding Stage 3 fusion.
builder.Services.AddSingleton<IFieldExtractor<TxtSource>, AdaptiveTxtFieldExtractor>();
// Multi-source companion extractors (MVP-PATH 2.1): feed XML and DOCX case files into Stage 3 fusion.
// XmlFieldExtractor has no extra dependencies; DocxFieldExtractor requires ILogger<DocxFieldExtractor>
// (resolved automatically by the DI container).
builder.Services.AddSingleton<IFieldExtractor<XmlSource>, XmlFieldExtractor>();
builder.Services.AddSingleton<IFieldExtractor<DocxSource>, DocxFieldExtractor>();

// Register orchestrator with all pipeline services
builder.Services.AddSingleton<ProcessingOrchestrator>(sp =>
{
    var eventPublisher = sp.GetRequiredService<IEventPublisher>();
    var logger = sp.GetRequiredService<ILogger<ProcessingOrchestrator>>();
    var fileLoader = sp.GetRequiredService<IFileLoader>();
    var qualityAnalyzer = sp.GetRequiredService<IImageQualityAnalyzer>();
    var ocrExecutor = sp.GetRequiredService<IOcrExecutor>();
    var fusionService = sp.GetRequiredService<IFusionExpediente>();
    var classifier = sp.GetRequiredService<IFileClassifier>();
    var exporter = sp.GetService<IResponseExporter>();
    var txtFieldExtractor = sp.GetRequiredService<IFieldExtractor<TxtSource>>();
    var xmlFieldExtractor = sp.GetRequiredService<IFieldExtractor<XmlSource>>();
    var docxFieldExtractor = sp.GetRequiredService<IFieldExtractor<DocxSource>>();

    return new ProcessingOrchestrator(
        eventPublisher,
        logger,
        qualityAnalyzer: qualityAnalyzer,
        ocrExecutor: ocrExecutor,
        fusionService: fusionService,
        classifier: classifier,
        exporter: exporter,
        fileLoader: fileLoader,
        txtFieldExtractor: txtFieldExtractor,
        xmlFieldExtractor: xmlFieldExtractor,
        docxFieldExtractor: docxFieldExtractor,
        scopeFactory: sp.GetService<IServiceScopeFactory>());
});

// Connection-level hub auth (follow-up to MVP-PATH 1.5): the reconciliation hub only accepts clients that
// present a valid JWT clearance token with ProcessClearance.Reconcile. The token is read from the
// access_token query-string parameter (standard SignalR JWT pattern — WebSocket upgrades cannot carry
// Authorization headers). The same ProcessIdentityOptions.JwtSecret signs both connection-level and
// per-message tokens.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/reconciliation"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
    });

// Authorization policy: the reconciliation hub requires the connecting client to carry
// ProcessClearance.Reconcile. Additive to the per-message clearance check in ReconciliationEventForwarder.
builder.Services.AddAuthorization(authOptions =>
    authOptions.AddPolicy(Prisma.Athena.Worker.Reconciliation.HubAuthPolicies.RequireReconcileClearance, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("clearance", ProcessClearance.Reconcile.ToString())));

// Reconciliator edge (MVP-PATH 1.4, ADR-011): the Athena worker is the Extractor actor. It runs the Extractor
// half (Quality→OCR→Fusion), persists the fused expediente to shared storage, and broadcasts an
// ExtractionCompletedEvent (carrying the storage-relative handoff path) to the downstream Reconciliator over a
// SignalR hub it hosts at /hubs/reconciliation. The broadcaster sends via IHubContext (a DI-resolved hub has a
// null Clients and cannot send). The handoff store reuses the shared-storage resolver registered above.
builder.Services.AddSingleton<IExpedienteHandoffStore, FileSystemExpedienteHandoffStore>();
builder.Services.AddSingleton<ExtractionOrchestrator>(sp => new ExtractionOrchestrator(
    sp.GetRequiredService<IEventPublisher>(),
    sp.GetRequiredService<ILogger<ExtractionOrchestrator>>(),
    qualityAnalyzer: sp.GetRequiredService<IImageQualityAnalyzer>(),
    ocrExecutor: sp.GetRequiredService<IOcrExecutor>(),
    fusionService: sp.GetRequiredService<IFusionExpediente>(),
    fileLoader: sp.GetRequiredService<IFileLoader>(),
    txtFieldExtractor: sp.GetRequiredService<IFieldExtractor<TxtSource>>(),
    xmlFieldExtractor: sp.GetRequiredService<IFieldExtractor<XmlSource>>(),
    docxFieldExtractor: sp.GetRequiredService<IFieldExtractor<DocxSource>>()));
// SmartEnum (EnumModel) JSON converter on the reconciliation hub protocol — same reason as the Orion
// ingestion edge: domain events on the wire carry SmartEnums that default System.Text.Json cannot
// reconstruct. Both ends of the edge must agree, so the Reconciliator client registers the same converter.
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new EnumModelJsonConverterFactory()));
builder.Services.AddSingleton<IExxerHub<ExtractionCompletedEvent>, SignalRReconciliationBroadcaster>();

// Per-process audit (MVP-PATH 1.6 A6): ExtractionPipelineService is singleton; IAuditLogger is scoped.
// The service resolves IAuditLogger per audit call via IServiceScopeFactory (no captive dependency).
builder.Services.AddSingleton<ExtractionPipelineService>(sp =>
{
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    var actorIdentityProvider = sp.GetService<ISiaraActorIdentityProvider>();
    var processOptions = sp.GetService<IOptions<ProcessIdentityOptions>>();
    var clearance = processOptions?.Value.Clearance ?? ProcessClearance.Extract;

    return new ExtractionPipelineService(
        sp.GetRequiredService<IEventPublisher>(),
        sp.GetRequiredService<ExtractionOrchestrator>(),
        sp.GetRequiredService<IExpedienteHandoffStore>(),
        sp.GetRequiredService<IExxerHub<ExtractionCompletedEvent>>(),
        sp.GetRequiredService<ILogger<ExtractionPipelineService>>(),
        scopeFactory: scopeFactory,
        actorIdentityProvider: actorIdentityProvider,
        processClearance: clearance,
        dashboardService: sp.GetRequiredService<IDashboardService>());
});
builder.Services.AddHostedService<AthenaWorkerService>();

// Readiness (MVP-PATH 4.2 / E1): the Extractor pipeline is the component this worker actually starts, so
// it is the truthful readiness signal. Resolve the same singleton instance behind IReadinessProbe.
builder.Services.AddSingleton<IReadinessProbe>(sp => sp.GetRequiredService<ExtractionPipelineService>());

// Register health check and dashboard services
builder.Services.AddSingleton<IHealthCheckService, AthenaHealthCheckService>();
builder.Services.AddSingleton<IDashboardService, AthenaDashboardService>();

var app = builder.Build();

// Hub auth middleware: must appear before MapHub so the JWT bearer scheme can authenticate the
// SignalR upgrade request before SignalR dispatches it to the hub.
app.UseAuthentication();
app.UseAuthorization();

// The reconciliation hub (MVP-PATH 1.4): downstream Reconciliators connect here to receive
// ExtractionCompletedEvent over the real Ember transport.
app.MapHub<ReconciliationHub>("/hubs/reconciliation");

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

// ── K8s init-container migration path ─────────────────────────────────────
// Run `--migrate-only` to apply EF Core migrations and exit without starting
// the normal host run loop. Intended for use as a K8s init-container:
//   command: ["./ExxerCube.Prisma.Athena.Worker", "--migrate-only"]
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

namespace Prisma.Athena.Worker
{
    /// <summary>
    /// Program class for Athena Worker (made public for WebApplicationFactory testing).
    /// </summary>
    public partial class Program { }
}
