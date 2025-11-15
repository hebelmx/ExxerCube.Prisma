namespace ExxerAI.RealTimeCommunication.Abstractions
{
    /// <summary>
    /// Factory for creating real-time communication connections
    /// </summary>
    public interface IConnectionFactory
    {
        /// <summary>
        /// Creates a new connection to the real-time communication server
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result containing the connection or failure information</returns>
        Task<Result<IConnection>> CreateConnectionAsync(CancellationToken cancellationToken = default);
    }
}