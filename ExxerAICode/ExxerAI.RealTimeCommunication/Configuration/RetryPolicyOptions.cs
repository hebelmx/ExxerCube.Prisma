namespace ExxerAI.RealTimeCommunication.Configuration
{
    /// <summary>
    /// Configuration options for retry policy
    /// </summary>
    public sealed class RetryPolicyOptions
    {
        /// <summary>
        /// Maximum number of retry attempts
        /// </summary>
        public int MaxRetries { get; init; } = 3;

        /// <summary>
        /// Base delay in milliseconds before the first retry
        /// </summary>
        public double BaseDelayMilliseconds { get; init; } = 1000; // 1 second

        /// <summary>
        /// Multiplier for exponential backoff
        /// </summary>
        public double BackoffMultiplier { get; init; } = 2.0; // Double each time

        /// <summary>
        /// Maximum delay in milliseconds between retries
        /// </summary>
        public double MaxDelayMilliseconds { get; init; } = 30000; // 30 seconds max

        /// <summary>
        /// Jitter factor to add randomness and prevent thundering herd (0.0 to 1.0)
        /// </summary>
        public double JitterFactor { get; init; } = 0.1; // 10% jitter

        /// <summary>
        /// Default retry policy options
        /// </summary>
        public static RetryPolicyOptions Default => new();

        /// <summary>
        /// Fast retry policy for quick operations
        /// </summary>
        public static RetryPolicyOptions Fast => new()
        {
            MaxRetries = 3,
            BaseDelayMilliseconds = 500,
            BackoffMultiplier = 1.5,
            MaxDelayMilliseconds = 5000,
            JitterFactor = 0.1
        };

        /// <summary>
        /// Aggressive retry policy for important operations
        /// </summary>
        public static RetryPolicyOptions Aggressive => new()
        {
            MaxRetries = 5,
            BaseDelayMilliseconds = 2000,
            BackoffMultiplier = 2.0,
            MaxDelayMilliseconds = 60000,
            JitterFactor = 0.2
        };
    }
}