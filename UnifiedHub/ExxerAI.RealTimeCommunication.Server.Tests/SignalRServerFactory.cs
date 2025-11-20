using ExxerAI.RealTimeCommunication.Server;
using ExxerAI.RealTimeCommunication.Server.Hubs;
using ExxerAI.RealTimeCommunication.Server.Services;
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Hubs;
using ExxerCube.Prisma.SignalR.Abstractions.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerAI.RealTimeCommunication.Server.Tests;

/// <summary>
/// WebApplicationFactory for SignalR server integration tests.
/// Provides a real test server with all hubs configured.
/// </summary>
/// <summary>
/// WebApplicationFactory for SignalR server integration tests.
/// Uses ProgramMarker since Program is a top-level class.
/// </summary>
public class SignalRServerFactory : WebApplicationFactory<ProgramMarker>
{
    /// <summary>
    /// Creates a SignalR hub connection to the test server.
    /// </summary>
    /// <param name="hubPath">The hub endpoint path (e.g., "/hubs/system")</param>
    /// <returns>Configured HubConnection ready to start</returns>
    public HubConnection CreateHubConnection(string hubPath)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(Server.BaseAddress + hubPath.TrimStart('/'), options =>
            {
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
            })
            .WithAutomaticReconnect()
            .Build();

        return connection;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove BackgroundMessageService for tests (it requires IHubContext which is complex to set up)
            // Tests will create their own hub connections directly
            var backgroundServiceDescriptor = services
                .FirstOrDefault(s => s.ServiceType == typeof(IHostedService) && 
                    s.ImplementationType == typeof(BackgroundMessageService));
            if (backgroundServiceDescriptor != null)
            {
                services.Remove(backgroundServiceDescriptor);
            }
        });
    }
}
