using ExxerAI.RealTimeCommunication.Server.Hubs;
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Dashboards;
using ExxerCube.Prisma.SignalR.Abstractions.Infrastructure.Connection;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace ExxerAI.RealTimeCommunication.Client.Dashboards;

/// <summary>
/// Health dashboard implementation demonstrating Dashboard&lt;T&gt; usage.
/// Receives and displays health updates from the SignalR hub.
/// </summary>
public class HealthDashboard : Dashboard<HealthUpdate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HealthDashboard"/> class.
    /// </summary>
    /// <param name="hubConnection">The SignalR hub connection.</param>
    /// <param name="reconnectionStrategy">The reconnection strategy.</param>
    /// <param name="logger">The logger instance.</param>
    public HealthDashboard(
        HubConnection hubConnection,
        ReconnectionStrategy? reconnectionStrategy,
        ILogger<HealthDashboard> logger)
        : base(hubConnection, reconnectionStrategy, logger)
    {
    }
}

