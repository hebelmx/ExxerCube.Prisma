using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Infrastructure.Classification;
using ExxerCube.Prisma.Infrastructure.Events;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using ExxerCube.Prisma.Infrastructure.Extraction.Txt;
using ExxerCube.Prisma.Infrastructure.FileSystem;
using ExxerCube.Prisma.Infrastructure.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Prisma.Athena.HealthChecks;
using Prisma.Athena.Processing;
using Prisma.Athena.Worker;

var builder = WebApplication.CreateBuilder(args);

// Register event infrastructure
builder.Services.AddSingleton<IEventPublisher, EventPublisher>();

// Register pipeline services (adapters → ports)
builder.Services.AddSingleton<IFileLoader, FileSystemLoader>();
builder.Services.AddSingleton<IImageQualityAnalyzer, PolynomialImageQualityAnalyzer>();
builder.Services.AddSingleton<IOcrExecutor, TesseractOcrExecutor>();
builder.Services.AddSingleton<IFusionExpediente, FusionExpedienteService>();
builder.Services.AddSingleton<IFileClassifier, FileClassifierService>();
builder.Services.AddSingleton<IAdaptiveExporter, AdaptiveExporter>();
// Field extractor that turns Stage 2 OCR text into an Expediente feeding Stage 3 fusion.
builder.Services.AddSingleton<IFieldExtractor<TxtSource>, AdaptiveTxtFieldExtractor>();

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
    var exporter = sp.GetRequiredService<IAdaptiveExporter>();
    var txtFieldExtractor = sp.GetRequiredService<IFieldExtractor<TxtSource>>();

    return new ProcessingOrchestrator(
        eventPublisher,
        logger,
        qualityAnalyzer: qualityAnalyzer,
        ocrExecutor: ocrExecutor,
        fusionService: fusionService,
        classifier: classifier,
        exporter: exporter,
        fileLoader: fileLoader,
        txtFieldExtractor: txtFieldExtractor);
});
builder.Services.AddHostedService<AthenaWorkerService>();

// Register health check and dashboard services
builder.Services.AddSingleton<IHealthCheckService, AthenaHealthCheckService>();
builder.Services.AddSingleton<IDashboardService, AthenaDashboardService>();

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

namespace Prisma.Athena.Worker
{
    /// <summary>
    /// Program class for Athena Worker (made public for WebApplicationFactory testing).
    /// </summary>
    public partial class Program { }
}
