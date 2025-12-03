using Microsoft.Extensions.DependencyInjection;
using Prisma.Athena.HealthChecks;
using Prisma.Athena.Processing;
using Prisma.Athena.Worker;

var builder = WebApplication.CreateBuilder(args);

// Register orchestrator and worker service
builder.Services.AddSingleton<ProcessingOrchestrator>();
builder.Services.AddHostedService<AthenaWorkerService>();

// Register health check and dashboard services
builder.Services.AddSingleton<IHealthCheckService, AthenaHealthCheckService>();
builder.Services.AddSingleton<IDashboardService, AthenaDashboardService>();

var app = builder.Build();

// Health endpoints
app.MapGet("/health", async (IHealthCheckService healthCheck, CancellationToken ct) =>
{
    var result = await healthCheck.GetHealthAsync(ct);
    return result.Status == HealthStatus.Healthy
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
    return result.Status == HealthStatus.Healthy
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

/// <summary>
/// Program class for Athena Worker (made public for WebApplicationFactory testing).
/// </summary>
public partial class Program { }