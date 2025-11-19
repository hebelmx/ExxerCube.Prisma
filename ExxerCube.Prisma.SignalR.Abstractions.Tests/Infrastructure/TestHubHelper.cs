using Microsoft.AspNetCore.SignalR;

namespace ExxerCube.Prisma.SignalR.Abstractions.Tests.Infrastructure;

/// <summary>
/// Helper class for setting up test hubs with mocked SignalR infrastructure.
/// </summary>
public static class TestHubHelper
{
    /// <summary>
    /// Sets up a hub with mocked context and clients for testing.
    /// </summary>
    /// <typeparam name="THub">The hub type.</typeparam>
    /// <param name="hub">The hub instance.</param>
    /// <param name="mockContext">The mocked hub caller context.</param>
    /// <param name="mockClients">The mocked hub caller clients.</param>
    /// <param name="mockGroups">The mocked group manager.</param>
    public static void SetupHub<THub>(
        THub hub,
        HubCallerContext mockContext,
        IHubCallerClients mockClients,
        IGroupManager mockGroups)
        where THub : Hub
    {
        // Use reflection to set protected properties
        var contextProperty = typeof(Hub).GetProperty("Context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var clientsProperty = typeof(Hub).GetProperty("Clients", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var groupsProperty = typeof(Hub).GetProperty("Groups", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        contextProperty?.SetValue(hub, mockContext);
        clientsProperty?.SetValue(hub, mockClients);
        groupsProperty?.SetValue(hub, mockGroups);
    }
}

