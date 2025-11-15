namespace ExxerAI.RealTimeCommunication.Abstractions
{
    /// <summary>
    /// Policy for retrying operations with exponential backoff
    /// </summary>
    public interface IRetryPolicy
    {
        /// <summary>
        /// Executes an operation with retry logic
        /// </summary>
        /// <typeparam name="T">The return type of the operation</typeparam>
        /// <param name="operation">The operation to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the operation after all retry attempts</returns>
        Task<Result<T>> ExecuteWithRetryAsync<T>(Func<Task<Result<T>>> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an operation with retry logic
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the operation after all retry attempts</returns>
        Task<Result> ExecuteWithRetryAsync(Func<Task<Result>> operation, CancellationToken cancellationToken = default);
    }
}