namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Represents a target for real-time communication
    /// </summary>
    public enum CommunicationTarget
    {
        /// <summary>
        /// System-wide communications
        /// </summary>
        System = 0,

        /// <summary>
        /// Agent-related communications
        /// </summary>
        Agent = 1,

        /// <summary>
        /// Task-related communications
        /// </summary>
        Task = 2,

        /// <summary>
        /// Document-related communications
        /// </summary>
        Document = 3,

        /// <summary>
        /// Economic-related communications
        /// </summary>
        Economic = 4
    }
}