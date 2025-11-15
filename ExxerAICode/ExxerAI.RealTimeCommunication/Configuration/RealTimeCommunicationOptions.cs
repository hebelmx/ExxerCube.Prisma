namespace ExxerAI.RealTimeCommunication.Configuration
{
    /// <summary>
    /// Configuration options for real-time communication
    /// </summary>
    public sealed class RealTimeCommunicationOptions
    {
        /// <summary>
        /// Configuration section name
        /// </summary>
        public const string SectionName = "RealTimeCommunication";

        /// <summary>
        /// The URL of the real-time communication server
        /// </summary>
        public string Url { get; init; } = "https://localhost:5001/hubs/communication";

        /// <summary>
        /// Whether to accept any server certificate (use only in development)
        /// </summary>
        public bool AcceptAnyServerCertificate { get; init; } = false;

        /// <summary>
        /// Retry interval in seconds for connection attempts
        /// </summary>
        public int RetryIntervalSeconds { get; init; } = 5;

        /// <summary>
        /// Maximum number of retry attempts for connection
        /// </summary>
        public int MaxRetryAttempts { get; init; } = 3;

        /// <summary>
        /// Connection timeout in seconds
        /// </summary>
        public int ConnectionTimeoutSeconds { get; init; } = 30;

        /// <summary>
        /// Whether to enable automatic reconnection
        /// </summary>
        public bool EnableAutomaticReconnect { get; init; } = true;

        /// <summary>
        /// The real-time communication provider to use
        /// </summary>
        public RealTimeProvider Provider { get; init; } = RealTimeProvider.SignalR;

        /// <summary>
        /// Additional provider-specific configuration
        /// </summary>
        public Dictionary<string, object> AdditionalOptions { get; init; } = new();
    }
}