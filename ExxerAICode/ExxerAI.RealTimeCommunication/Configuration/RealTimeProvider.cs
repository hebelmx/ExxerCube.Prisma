namespace ExxerAI.RealTimeCommunication.Configuration
{
    /// <summary>
    /// Available real-time communication providers
    /// </summary>
    public enum RealTimeProvider
    {
        /// <summary>
        /// SignalR provider (default)
        /// </summary>
        SignalR = 0,

        /// <summary>
        /// WebSocket provider
        /// </summary>
        WebSocket = 1,

        /// <summary>
        /// gRPC streaming provider
        /// </summary>
        GrpcStreaming = 2
    }
}