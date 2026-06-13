using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Classification;
using ExxerCube.Prisma.Infrastructure.Events;
using ExxerCube.Prisma.Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Prisma.Athena.Processing;
using Prisma.Athena.Processing.Reconciliation;
using Prisma.Reconciliator.Worker;

var builder = WebApplication.CreateBuilder(args);

// The Reconciliator actor of the 3-process split (MVP-PATH 1.4, ADR-011). It is a SignalR CLIENT of the Athena
// Extractor's /hubs/reconciliation: it receives ExtractionCompletedEvent, loads the fused expediente from the
// shared volume, and runs Classification → Export.
builder.Services.AddSingleton<IEventPublisher, EventPublisher>();

// Cross-process reconciliation subscriber: connect to the Athena reconciliation hub and republish each
// ExtractionCompletedEvent onto the local stream the pipeline subscribes to.
builder.Services.Configure<ReconciliationClientOptions>(
    builder.Configuration.GetSection(ReconciliationClientOptions.SectionName));
builder.Services.AddSingleton<ReconciliationEventForwarder>();
builder.Services.AddHostedService<ReconciliationHubClient>();

// Shared document storage (ADR-011): the Reconciliator mounts the same volume the Extractor wrote the fused
// expediente handoff to, possibly at a different absolute path, and resolves the event's storage-relative path
// against its own configured base before loading the handoff artifact.
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddSingleton<IStoragePathResolver, SharedStoragePathResolver>();
builder.Services.AddSingleton<IExpedienteHandoffStore, FileSystemExpedienteHandoffStore>();

// Reconciliator pipeline services. Classification is wired; Export (IAdaptiveExporter) is optional — the
// adaptive exporter is backed by a template database, so a deployment enables SIRO export by also registering
// AddAdaptiveExportServices(connectionString). When absent, Stage 5 is skipped with a warning (the Reconciliator
// still classifies and emits the terminal completion event).
builder.Services.AddSingleton<IFileClassifier, FileClassifierService>();
builder.Services.AddSingleton<ReconciliationOrchestrator>(sp => new ReconciliationOrchestrator(
    sp.GetRequiredService<IEventPublisher>(),
    sp.GetRequiredService<ILogger<ReconciliationOrchestrator>>(),
    classifier: sp.GetRequiredService<IFileClassifier>(),
    exporter: sp.GetService<IAdaptiveExporter>()));
builder.Services.AddSingleton<ReconciliationPipelineService>();
builder.Services.AddHostedService<ReconciliatorWorkerService>();

var app = builder.Build();

// Health endpoints (minimal: the Reconciliator is a background subscriber with no orchestrator readiness gate yet).
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

await app.RunAsync();

namespace Prisma.Reconciliator.Worker
{
    /// <summary>
    /// Program class for the Reconciliator Worker (made public for WebApplicationFactory testing).
    /// </summary>
    public partial class Program { }
}
