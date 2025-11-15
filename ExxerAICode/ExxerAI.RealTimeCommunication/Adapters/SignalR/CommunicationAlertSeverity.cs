namespace ExxerAI.RealTimeCommunication.Adapters.SignalR
{
    /// <summary>
    /// Defines severity levels for communication alerts
    /// </summary>
    public enum CommunicationAlertSeverity
    {
        /// <summary>
        /// Indicates a critical alert that requires immediate attention
        /// </summary>
        Critical,

        /// <summary>
        /// Gets or sets the error information associated with the current context.
        /// </summary>
        Error,

        /// <summary>
        /// Indicates a warning alert that should be noted but is not critical
        /// </summary>

        Warning,

        /// <summary>
        /// Indicates an informational alert that provides context but is not an error.
        /// </summary>
        Informational,
    }
}
