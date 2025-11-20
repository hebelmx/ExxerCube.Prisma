using ExxerCube.Prisma.SignalR.Abstractions.Infrastructure.Connection;

namespace ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Dashboards;

/// <summary>
/// Interface for dashboard components that display real-time data via SignalR.
/// Provides lifecycle management and connection state tracking.
/// </summary>
/// <typeparam name="T">The type of data displayed in the dashboard.</typeparam>
public interface IDashboard<T>
{
    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    ConnectionState ConnectionState { get; }

    /// <summary>
    /// Gets the current data collection.
    /// </summary>
    IReadOnlyList<T> Data { get; }

    /// <summary>
    /// Connects to the SignalR hub.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the SignalR hub.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when connection state changes.
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Event raised when new data is received.
    /// </summary>
    event EventHandler<DataReceivedEventArgs<T>>? DataReceived;
}

/// <summary>
/// Event arguments for connection state change events.
/// </summary>
public class ConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous connection state.
    /// </summary>
    public ConnectionState PreviousState { get; }

    /// <summary>
    /// Gets the new connection state.
    /// </summary>
    public ConnectionState NewState { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionStateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="previousState">The previous connection state.</param>
    /// <param name="newState">The new connection state.</param>
    public ConnectionStateChangedEventArgs(ConnectionState previousState, ConnectionState newState)
    {
        PreviousState = previousState;
        NewState = newState;
    }
}

/// <summary>
/// Event arguments for data received events.
/// </summary>
/// <typeparam name="T">The type of data received.</typeparam>
public class DataReceivedEventArgs<T> : EventArgs
{
    /// <summary>
    /// Gets the received data.
    /// </summary>
    public T Data { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataReceivedEventArgs{T}"/> class.
    /// </summary>
    /// <param name="data">The received data.</param>
    public DataReceivedEventArgs(T data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }
}

