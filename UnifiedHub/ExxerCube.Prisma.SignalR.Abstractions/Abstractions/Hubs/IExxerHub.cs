namespace ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Hubs;

/// <summary>
/// Interface for SignalR hub abstraction following Hexagonal Architecture principles.
/// Provides a generic, type-safe abstraction for SignalR hubs with Railway-Oriented Programming support.
/// </summary>
/// <typeparam name="T">The type of data transmitted through the hub.</typeparam>
public interface IExxerHub<T>
{
    /// <summary>
    /// Sends data to all connected clients.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> SendToAllAsync(T data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends data to a specific client by connection ID.
    /// </summary>
    /// <param name="connectionId">The connection ID of the target client.</param>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> SendToClientAsync(string connectionId, T data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends data to a group of clients.
    /// </summary>
    /// <param name="groupName">The name of the group.</param>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> SendToGroupAsync(string groupName, T data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current connection count.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the connection count.</returns>
    Task<Result<int>> GetConnectionCountAsync(CancellationToken cancellationToken = default);
}

