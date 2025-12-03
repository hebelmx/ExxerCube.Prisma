using Microsoft.Extensions.DependencyInjection;
using Prisma.Orion.HealthChecks;
using Prisma.Orion.Ingestion;
using Prisma.Orion.Worker;

var builder = WebApplication.CreateBuilder(args);

// Register orchestrator and worker service
builder.Services.AddSingleton<IngestionOrchestrator>();
builder.Services.AddHostedService<OrionWorkerService>();

// Register health check and dashboard services
builder.Services.AddSingleton<IHealthCheckService, OrionHealthCheckService>();
builder.Services.AddSingleton<IDashboardService, OrionDashboardService>();

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
/// Program class for Orion Worker (made public for WebApplicationFactory testing).
/// </summary>
public partial class Program { }