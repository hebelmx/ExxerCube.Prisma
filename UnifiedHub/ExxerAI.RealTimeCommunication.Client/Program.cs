using ExxerAI.RealTimeCommunication.Client.Dashboards;
using ExxerAI.RealTimeCommunication.Client.Health;
using ExxerAI.RealTimeCommunication.Client.Services;
using ExxerAI.RealTimeCommunication.Server.Hubs;
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Dashboards;
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Health;
using ExxerCube.Prisma.SignalR.Abstractions.Extensions;
using ExxerCube.Prisma.SignalR.Abstractions.Infrastructure.Connection;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddLogging(configure => configure.AddConsole().SetMinimumLevel(LogLevel.Information));
builder.Services.AddSignalRAbstractions();

// Get server URL from configuration or use default
var serverUrl = builder.Configuration["ServerUrl"] ?? "http://localhost:5000";

// Create hub connections
var systemHubConnection = new HubConnectionBuilder()
    .WithUrl($"{serverUrl}/hubs/system")
    .WithAutomaticReconnect()
    .Build();

var healthHubConnection = new HubConnectionBuilder()
    .WithUrl($"{serverUrl}/hubs/health")
    .WithAutomaticReconnect()
    .Build();

// Register dashboard and health monitoring
var reconnectionStrategy = new ReconnectionStrategy();
builder.Services.AddSingleton(reconnectionStrategy);

// Register dashboards
builder.Services.AddSingleton<IDashboard<SystemMessage>>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SystemDashboard>>();
    return new SystemDashboard(systemHubConnection, reconnectionStrategy, logger);
});

builder.Services.AddSingleton<IDashboard<HealthUpdate>>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<HealthDashboard>>();
    return new HealthDashboard(healthHubConnection, reconnectionStrategy, logger);
});

// Register health monitoring
builder.Services.AddSingleton<IServiceHealth<HealthStatusData>>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ClientServiceHealth>>();
    return new ClientServiceHealth(logger);
});

// Add background service to demonstrate dashboard usage
builder.Services.AddHostedService<DashboardBackgroundService>();

var app = builder.Build();

// Simple status endpoint
app.MapGet("/", () => Results.Ok(new
{
    status = "Client running",
    connectedTo = serverUrl,
    hubs = new[]
    {
        "/hubs/system",
        "/hubs/health"
    }
}));

app.Run();

/// <summary>
/// Entry point class made accessible for integration testing via WebApplicationFactory
/// </summary>
public partial class Program { }
