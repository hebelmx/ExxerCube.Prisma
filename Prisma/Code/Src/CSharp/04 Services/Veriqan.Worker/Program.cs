using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Adapters;
using ExxerCube.Prisma.Veriqan.Orchestration.Batch;
using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Worker;
using ExxerCube.Prisma.Veriqan.Worker.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Veriqan VEC batch worker — full DI composition via Orchestration layer.
builder.Services.AddVeriqan(builder.Configuration);

// Health-check service — the SqlLegalToleranceProvider is optional (only present when SQL
// persistence is configured via ConnectionStrings:VeriqanDb). We resolve it as the concrete
// type directly so the check can read IsWarm; the interface does not expose that property.
builder.Services.AddSingleton<IHealthCheckService>(sp =>
{
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    // Null when running with in-memory persistence (no connection string).
    var toleranceProvider = sp.GetService<SqlLegalToleranceProvider>();
    var csvOptions = sp.GetRequiredService<IOptions<CsvReferenceDataOptions>>();
    var logger = sp.GetRequiredService<ILogger<VeriqanWorkerHealthCheckService>>();
    return new VeriqanWorkerHealthCheckService(scopeFactory, toleranceProvider, csvOptions, logger);
});

var app = builder.Build();

// Health endpoints — backed by VeriqanWorkerHealthCheckService (three readiness checks).
// /health/live → liveness only: process-up, no external deps.
// /health/ready → all three readiness checks; 503 when any check fails.
// /health       → combined (same readiness set); kept for backward compatibility.
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

app.MapGet("/health", async (IHealthCheckService healthCheck, CancellationToken ct) =>
{
    var result = await healthCheck.GetHealthAsync(ct);
    return result.Status == OrchestratorHealthState.Healthy
        ? Results.Ok(new { status = result.Status.ToString(), description = result.Description, data = result.Data })
        : Results.Json(new { status = result.Status.ToString(), description = result.Description, data = result.Data }, statusCode: 503);
});

// POST /verify — single-statement verification.
// TODO(E4): add authentication/authorisation before production deployment.
app.MapPost("/verify", async (
    [FromBody] StatementSubmission submission,
    [FromServices] IVerificationPipeline pipeline,
    HttpContext httpContext) =>
{
    var ct = httpContext.RequestAborted;

    if (ct.IsCancellationRequested)
        return Results.StatusCode(499); // client closed request

    var result = await pipeline.ProcessAsync(submission, ct);

    if (!result.IsSuccess)
        return Results.UnprocessableEntity(new { error = result.Error });

    var jobId = result.Value!.Job.Id;
    return Results.Accepted(value: new { jobId });
});

// POST /batch — multi-statement batch verification.
// TODO(E4): add authentication/authorisation before production deployment.
app.MapPost("/batch", async (
    [FromBody] BatchSubmissionRequest request,
    [FromServices] IBatchProcessor batchProcessor,
    HttpContext httpContext) =>
{
    var ct = httpContext.RequestAborted;

    if (ct.IsCancellationRequested)
        return Results.StatusCode(499); // client closed request

    var options = request.Options ?? new BatchOptions();
    var result = await batchProcessor.ProcessBatchAsync(request.Items, options, progress: null, ct);

    if (!result.IsSuccess)
        return Results.UnprocessableEntity(new { error = result.Error });

    var report = result.Value!;
    return Results.Accepted(value: new
    {
        totalSubmitted = report.TotalSubmitted,
        completedCount = report.CompletedCount,
        failedCount = report.FailedCount,
    });
});

await app.RunAsync();

namespace ExxerCube.Prisma.Veriqan.Worker
{
    /// <summary>
    /// Program entry point for the Veriqan VEC worker (made public for WebApplicationFactory testing).
    /// </summary>
    public partial class Program { }
}
