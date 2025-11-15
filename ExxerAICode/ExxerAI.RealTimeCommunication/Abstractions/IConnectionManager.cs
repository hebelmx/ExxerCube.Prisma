namespace ExxerAI.RealTimeCommunication.Abstractions
{
    /// <summary>
    /// Manager for real-time communication connections with retry logic and state management
    /// </summary>
    public interface IConnectionManager : IAsyncDisposable
    {
        /// <summary>
        /// Ensures a valid connection is available, creating one if necessary
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result containing the connection or failure information</returns>
        Task<Result<IConnection>> EnsureConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that the current connection is in a good state
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating whether the connection is valid</returns>
        Task<Result> ValidateConnectionAsync(CancellationToken cancellationToken = default);
    }
}