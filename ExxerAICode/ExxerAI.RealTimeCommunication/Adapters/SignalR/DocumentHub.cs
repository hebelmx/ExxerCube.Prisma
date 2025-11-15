namespace ExxerAI.RealTimeCommunication.Adapters.SignalR
{
    /// <summary>
    /// SignalR hub for document-related communications
    /// </summary>
    public sealed class DocumentHub : BaseHub<DocumentHub>
    {
        /// <summary>
        /// Initializes a new instance of the DocumentHub
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public DocumentHub(ILogger<DocumentHub> logger) : base(logger)
        {
        }

        /// <summary>
        /// Joins a document processing group
        /// </summary>
        /// <param name="documentId">The ID of the document</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinDocumentGroupAsync(string documentId)
        {
            var groupName = $"Document_{documentId}";
            await AddToGroupAsync(groupName);
            Logger.LogInformation("Connection {ConnectionId} joined document group {GroupName}", 
                Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Leaves a document processing group
        /// </summary>
        /// <param name="documentId">The ID of the document</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveDocumentGroupAsync(string documentId)
        {
            var groupName = $"Document_{documentId}";
            await RemoveFromGroupAsync(groupName);
            Logger.LogInformation("Connection {ConnectionId} left document group {GroupName}", 
                Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Joins the document processing monitoring group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinDocumentProcessingMonitoringAsync()
        {
            await AddToGroupAsync("DocumentProcessingMonitoring");
            Logger.LogInformation("Connection {ConnectionId} joined DocumentProcessingMonitoring group", Context.ConnectionId);
        }

        /// <summary>
        /// Leaves the document processing monitoring group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveDocumentProcessingMonitoringAsync()
        {
            await RemoveFromGroupAsync("DocumentProcessingMonitoring");
            Logger.LogInformation("Connection {ConnectionId} left DocumentProcessingMonitoring group", Context.ConnectionId);
        }

        /// <summary>
        /// Joins the document ingestion group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinDocumentIngestionAsync()
        {
            await AddToGroupAsync("DocumentIngestion");
            Logger.LogInformation("Connection {ConnectionId} joined DocumentIngestion group", Context.ConnectionId);
        }

        /// <summary>
        /// Leaves the document ingestion group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveDocumentIngestionAsync()
        {
            await RemoveFromGroupAsync("DocumentIngestion");
            Logger.LogInformation("Connection {ConnectionId} left DocumentIngestion group", Context.ConnectionId);
        }
    }
}