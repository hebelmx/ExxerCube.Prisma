using ExxerAI.RealTimeCommunication.Server.Hubs;
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Dashboards;
using ExxerCube.Prisma.SignalR.Abstractions.Infrastructure.Connection;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace ExxerAI.RealTimeCommunication.Client.Dashboards;

/// <summary>
/// System dashboard implementation demonstrating Dashboard&lt;T&gt; usage.
/// Receives and displays system messages from the SignalR hub.
/// </summary>
public class SystemDashboard : Dashboard<SystemMessage>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SystemDashboard"/> class.
    /// </summary>
    /// <param name="hubConnection">The SignalR hub connection.</param>
    /// <param name="reconnectionStrategy">The reconnection strategy.</param>
    /// <param name="logger">The logger instance.</param>
    public SystemDashboard(
        HubConnection hubConnection,
        ReconnectionStrategy? reconnectionStrategy,
        ILogger<SystemDashboard> logger)
        : base(hubConnection, reconnectionStrategy, logger)
    {
    }
}

