using ExxerAI.RealTimeCommunication.Server.Hubs;
using ExxerAI.RealTimeCommunication.Server.Services;
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Hubs;
using ExxerCube.Prisma.SignalR.Abstractions.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add SignalR services
builder.Services.AddSignalR();

// Add SignalR Abstractions services
builder.Services.AddSignalRAbstractions();

// Register hub implementations
builder.Services.AddScoped<TestSystemHub>();
builder.Services.AddScoped<TestHealthHub>();

// Register hub interfaces for dependency injection
builder.Services.AddScoped<IExxerHub<SystemMessage>>(sp => sp.GetRequiredService<TestSystemHub>());
builder.Services.AddScoped<IExxerHub<HealthUpdate>>(sp => sp.GetRequiredService<TestHealthHub>());

// Register system diagnostics collector
builder.Services.AddSingleton<SystemDiagnosticsCollector>();

// Add background service for testing
builder.Services.AddHostedService<BackgroundMessageService>();

// Add CORS for dashboard
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Enable static files for dashboard
app.UseDefaultFiles();
app.UseStaticFiles();

// Enable CORS
app.UseCors();

// Map SignalR hubs - Test implementations for integration testing and examples
app.MapHub<TestSystemHub>("/hubs/system");
app.MapHub<TestHealthHub>("/hubs/health");

// Simple health check endpoint
app.MapGet("/", () => "ExxerAI RealTime Communication Server - Running");
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
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
