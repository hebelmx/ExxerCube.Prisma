namespace ExxerAI.RealTimeCommunication.Abstractions
{
    /// <summary>
    /// Port for event broadcasting operations - infrastructure-agnostic
    /// </summary>
    public interface IEventBroadcastingPort
    {
        /// <summary>
        /// Broadcasts a system event to all clients
        /// </summary>
        /// <param name="systemEvent">The system event</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> BroadcastSystemEventAsync(SystemEvent systemEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Broadcasts an agent event to all clients
        /// </summary>
        /// <param name="agentEvent">The agent event</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> BroadcastAgentEventAsync(AgentEvent agentEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Broadcasts a task event to all clients
        /// </summary>
        /// <param name="taskEvent">The task event</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> BroadcastTaskEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Broadcasts a document event to all clients
        /// </summary>
        /// <param name="documentEvent">The document event</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> BroadcastDocumentEventAsync(DocumentEvent documentEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Broadcasts an economic event to all clients
        /// </summary>
        /// <param name="economicEvent">The economic event</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> BroadcastEconomicEventAsync(EconomicEvent economicEvent, CancellationToken cancellationToken = default);
    }
}