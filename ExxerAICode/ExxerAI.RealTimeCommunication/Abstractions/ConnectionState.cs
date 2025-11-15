namespace ExxerAI.RealTimeCommunication.Abstractions
{
    /// <summary>
    /// Represents the state of a real-time communication connection
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// The connection is disconnected
        /// </summary>
        Disconnected = 0,

        /// <summary>
        /// The connection is currently connecting
        /// </summary>
        Connecting = 1,

        /// <summary>
        /// The connection is connected and ready to send messages
        /// </summary>
        Connected = 2,

        /// <summary>
        /// The connection is reconnecting after being disconnected
        /// </summary>
        Reconnecting = 3
    }
}