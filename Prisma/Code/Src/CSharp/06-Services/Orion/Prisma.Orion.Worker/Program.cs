using ExxerCube.Prisma.Domain.Events;
using IndFusion.Ember.Abstractions.Hubs;
using Microsoft.Extensions.DependencyInjection;
using Prisma.Orion.HealthChecks;
using Prisma.Orion.Ingestion;
using Prisma.Orion.Worker;

var builder = WebApplication.CreateBuilder(args);

// Register orchestrator dependencies
builder.Services.AddSingleton<IIngestionJournal>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<FileIngestionJournal>>();
    var journalPath = Path.Combine(Directory.GetCurrentDirectory(), "journal.txt");
    return new FileIngestionJournal(journalPath, logger);
});
builder.Services.AddSingleton<IDocumentDownloader, StubDocumentDownloader>();

// Register event hub (stub for now - replace with actual SignalR hub in production)
builder.Services.AddSingleton<IExxerHub<DocumentDownloadedEvent>, StubExxerHub<DocumentDownloadedEvent>>();

// Register orchestrator and worker service
builder.Services.AddSingleton<IngestionOrchestrator>(sp =>
{
    var journal = sp.GetRequiredService<IIngestionJournal>();
    var downloader = sp.GetRequiredService<IDocumentDownloader>();
    var eventHub = sp.GetRequiredService<IExxerHub<DocumentDownloadedEvent>>();
    var logger = sp.GetRequiredService<ILogger<IngestionOrchestrator>>();
    return new IngestionOrchestrator(journal, downloader, eventHub, logger);
});
builder.Services.AddHostedService<OrionWorkerService>();

// Register health check and dashboard services
builder.Services.AddSingleton<IHealthCheckService, OrionHealthCheckService>();
builder.Services.AddSingleton<IDashboardService, OrionDashboardService>();

var app = builder.Build();

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