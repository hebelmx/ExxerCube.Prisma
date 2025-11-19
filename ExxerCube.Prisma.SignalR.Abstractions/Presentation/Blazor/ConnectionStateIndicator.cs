using ExxerCube.Prisma.SignalR.Abstractions.Infrastructure.Connection;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ExxerCube.Prisma.SignalR.Abstractions.Presentation.Blazor;

/// <summary>
/// Blazor component that displays the current SignalR connection state.
/// </summary>
public partial class ConnectionStateIndicator : ComponentBase
{
    /// <summary>
    /// Gets or sets the current connection state.
    /// </summary>
    [Parameter]
    public ConnectionState State { get; set; }

    /// <summary>
    /// Gets the color for the connection state indicator.
    /// </summary>
    private Color GetStateColor() => State switch
    {
        ConnectionState.Connected => Color.Success,
        ConnectionState.Connecting => Color.Warning,
        ConnectionState.Reconnecting => Color.Warning,
        ConnectionState.Disconnected => Color.Default,
        ConnectionState.Failed => Color.Error,
        _ => Color.Default
    };

    /// <summary>
    /// Gets the icon for the connection state indicator.
    /// </summary>
    private string GetStateIcon() => State switch
    {
        ConnectionState.Connected => Icons.Material.Filled.CheckCircle,
        ConnectionState.Connecting => Icons.Material.Filled.Sync,
        ConnectionState.Reconnecting => Icons.Material.Filled.Sync,
        ConnectionState.Disconnected => Icons.Material.Filled.Cancel,
        ConnectionState.Failed => Icons.Material.Filled.Error,
        _ => Icons.Material.Filled.Help
    };

    /// <summary>
    /// Gets the text for the connection state indicator.
    /// </summary>
    private string GetStateText() => State switch
    {
        ConnectionState.Connected => "Connected",
        ConnectionState.Connecting => "Connecting...",
        ConnectionState.Reconnecting => "Reconnecting...",
        ConnectionState.Disconnected => "Disconnected",
        ConnectionState.Failed => "Connection Failed",
        _ => "Unknown"
    };
}

