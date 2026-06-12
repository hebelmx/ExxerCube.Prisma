using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.NavigationTargets;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;
using IndFusion.Ember.Abstractions.Hubs;
using Microsoft.Extensions.DependencyInjection;
using Prisma.Orion.HealthChecks;
using Prisma.Orion.Ingestion;
using Prisma.Orion.Worker;
using Prisma.Orion.Worker.Ingestion;

var builder = WebApplication.CreateBuilder(args);

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

// The real downloader is scoped: it rides a scoped Playwright browser and SIARA session. The watch loop
// creates a fresh scope per document (SiaraWatchLoop), so the singleton host never captures it.
builder.Services.AddScoped<IDocumentDownloader, SiaraDocumentDownloader>();

// The discovery source (MVP-PATH 1.2 — the "list" half) is scoped for the same reason; the watch loop
// keeps one long-lived discovery scope so the SIARA session stays warm across cycles.
builder.Services.AddScoped<ISiaraDocumentSource, SiaraDocumentSource>();

// The real IndFusion.Ember transport (MVP-PATH 1.3): the Orion Downloader actor hosts a SignalR hub and
// broadcasts DocumentDownloadedEvent to the downstream Extractor (Athena) — the first cross-process edge of
// the Three-Actors split (ADR-009/ADR-011). Replaces StubExxerHub. The broadcaster sends via IHubContext
// (a DI-resolved hub instance has a null Clients and cannot send).
builder.Services.AddSignalR();
builder.Services.AddSingleton<IExxerHub<DocumentDownloadedEvent>, SignalRIngestionBroadcaster>();

// The orchestrator is scoped because it depends on the scoped downloader; the worker resolves it inside
// a per-work scope and the health/dashboard endpoints resolve it from the per-request scope.
builder.Services.AddScoped<IngestionOrchestrator>(sp =>
{
    var journal = sp.GetRequiredService<IIngestionJournal>();
    var downloader = sp.GetRequiredService<IDocumentDownloader>();
    var eventHub = sp.GetRequiredService<IExxerHub<DocumentDownloadedEvent>>();
    var logger = sp.GetRequiredService<ILogger<IngestionOrchestrator>>();
    return new IngestionOrchestrator(journal, downloader, eventHub, logger);
});

// The SIARA watch loop (MVP-PATH 1.2): a singleton poll/watcher that owns the DI scopes (one warm
// discovery scope + a fresh scope per document). Driven by the hosted worker.
builder.Services.Configure<WatchLoopOptions>(
    builder.Configuration.GetSection(WatchLoopOptions.SectionName));
builder.Services.AddSingleton<SiaraWatchLoop>();
builder.Services.AddHostedService<OrionWorkerService>();

// Health + dashboard depend (transitively) on the scoped orchestrator, so they are scoped too; the
// minimal-API endpoints resolve them from the per-request scope.
builder.Services.AddScoped<IHealthCheckService, OrionHealthCheckService>();
builder.Services.AddScoped<IDashboardService, OrionDashboardService>();

var app = builder.Build();

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