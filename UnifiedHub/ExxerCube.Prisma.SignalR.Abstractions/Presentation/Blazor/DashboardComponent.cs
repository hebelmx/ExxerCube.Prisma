using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Dashboards;
using ExxerCube.Prisma.SignalR.Abstractions.Infrastructure.Connection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.SignalR.Abstractions.Presentation.Blazor;

/// <summary>
/// Base Blazor component for dashboards that display real-time data via SignalR.
/// Provides automatic connection management and lifecycle handling.
/// </summary>
/// <typeparam name="T">The type of data displayed in the dashboard.</typeparam>
public abstract class DashboardComponent<T> : ComponentBase, IAsyncDisposable
{
    private Dashboard<T>? _dashboard;
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    /// <summary>
    /// Gets or sets the SignalR hub connection.
    /// </summary>
    [Parameter]
    public HubConnection? HubConnection { get; set; }

    /// <summary>
    /// Gets or sets the reconnection strategy.
    /// </summary>
    [Parameter]
    public Infrastructure.Connection.ReconnectionStrategy? ReconnectionStrategy { get; set; }

    /// <summary>
    /// Gets the logger instance.
    /// </summary>
    [Inject]
    protected ILogger<DashboardComponent<T>> Logger { get; set; } = null!;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    protected ConnectionState ConnectionState => _connectionState;

    /// <summary>
    /// Gets the current data collection.
    /// </summary>
    protected IReadOnlyList<T> Data => _dashboard?.Data ?? Array.Empty<T>();

    /// <summary>
    /// Called when the component is initialized.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync().ConfigureAwait(false);

        if (HubConnection != null)
        {
            _dashboard = CreateDashboard(HubConnection, ReconnectionStrategy, Logger);
            _dashboard.ConnectionStateChanged += OnConnectionStateChanged;
            _dashboard.DataReceived += OnDataReceived;

            var connectResult = await _dashboard.ConnectAsync().ConfigureAwait(false);
            if (connectResult.IsFailure)
            {
                Logger.LogError("Failed to connect dashboard: {Error}", connectResult.Error);
            }
        }
    }

    /// <summary>
    /// Creates a dashboard instance. Override to provide custom dashboard implementation.
    /// </summary>
    /// <param name="hubConnection">The hub connection.</param>
    /// <param name="reconnectionStrategy">The reconnection strategy.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>A dashboard instance.</returns>
    protected abstract Dashboard<T> CreateDashboard(
        HubConnection hubConnection,
        Infrastructure.Connection.ReconnectionStrategy? reconnectionStrategy,
        ILogger<DashboardComponent<T>> logger);

    /// <summary>
    /// Called when connection state changes.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        _connectionState = e.NewState;
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Called when new data is received.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnDataReceived(object? sender, DataReceivedEventArgs<T> e)
    {
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Disposes the component and disconnects from the hub.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        if (_dashboard != null)
        {
            _dashboard.ConnectionStateChanged -= OnConnectionStateChanged;
            _dashboard.DataReceived -= OnDataReceived;
            await _dashboard.DisposeAsync().ConfigureAwait(false);
        }
    }
}

