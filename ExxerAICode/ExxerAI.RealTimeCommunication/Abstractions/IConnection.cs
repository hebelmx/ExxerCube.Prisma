namespace ExxerAI.RealTimeCommunication.Abstractions
{
    /// <summary>
    /// Represents a real-time communication connection
    /// Abstracted from specific implementations (SignalR, WebSocket, etc.)
    /// </summary>
    public interface IConnection : IAsyncDisposable
    {
        /// <summary>
        /// Gets the current state of the connection
        /// </summary>
        ConnectionState State { get; }

        /// <summary>
        /// Gets the connection ID if connected, null otherwise
        /// </summary>
        string? ConnectionId { get; }

        /// <summary>
        /// Sends a message to the server without expecting a response
        /// </summary>
        /// <param name="methodName">The name of the method to invoke</param>
        /// <param name="arguments">The arguments to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> SendAsync(string methodName, object?[] arguments, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method on the server and waits for completion
        /// </summary>
        /// <param name="methodName">The name of the method to invoke</param>
        /// <param name="arguments">The arguments to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> InvokeAsync(string methodName, object?[] arguments, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a method on the server and returns the result
        /// </summary>
        /// <typeparam name="T">The type of the expected return value</typeparam>
        /// <param name="methodName">The name of the method to invoke</param>
        /// <param name="arguments">The arguments to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result containing the return value or failure information</returns>
        Task<Result<T>> InvokeAsync<T>(string methodName, object?[] arguments, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the connection
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the connection
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Event raised when the connection is closed
        /// </summary>
        event Func<Exception?, Task>? Closed;

        /// <summary>
        /// Event raised when the connection is reconnected
        /// </summary>
        event Func<string?, Task>? Reconnected;

        /// <summary>
        /// Event raised when the connection starts reconnecting
        /// </summary>
        event Func<Exception?, Task>? Reconnecting;
    }
}