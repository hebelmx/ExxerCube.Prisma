namespace ExxerAI.RealTimeCommunication.Adapters.SignalR
{
    /// <summary>
    /// SignalR adapter implementing real-time communication ports
    /// Borrows and improves patterns from IndTrace industrial solution
    /// </summary>
    public sealed class SignalRAdapter : ICompleteCommunicationPort, IAsyncDisposable, IDisposable
    {
        private readonly IHubContext<SystemHub> _systemHub;
        private readonly IHubContext<AgentHub> _agentHub;
        private readonly IHubContext<TaskHub> _taskHub;
        private readonly IHubContext<DocumentHub> _documentHub;
        private readonly IHubContext<EconomicHub> _economicHub;
        private readonly ILogger<SignalRAdapter> _logger;

        // Enhanced IndTrace pattern: Rate-limited logging with memory leak prevention
        private readonly SemaphoreSlim _connectionSemaphore;

        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the SignalRAdapter
        /// </summary>
        /// <param name="systemHub">System hub context</param>
        /// <param name="agentHub">Agent hub context</param>
        /// <param name="taskHub">Task hub context</param>
        /// <param name="documentHub">Document hub context</param>
        /// <param name="economicHub">Economic hub context</param>
        /// <param name="logger">Logger instance</param>
        public SignalRAdapter(
            IHubContext<SystemHub> systemHub,
            IHubContext<AgentHub> agentHub,
            IHubContext<TaskHub> taskHub,
            IHubContext<DocumentHub> documentHub,
            IHubContext<EconomicHub> economicHub,
            ILogger<SignalRAdapter> logger)
        {
            _systemHub = systemHub ?? throw new ArgumentNullException(nameof(systemHub));
            _agentHub = agentHub ?? throw new ArgumentNullException(nameof(agentHub));
            _taskHub = taskHub ?? throw new ArgumentNullException(nameof(taskHub));
            _documentHub = documentHub ?? throw new ArgumentNullException(nameof(documentHub));
            _economicHub = economicHub ?? throw new ArgumentNullException(nameof(economicHub));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // IndTrace pattern: Thread-safe connection management
            _connectionSemaphore = new SemaphoreSlim(1, 1);
        }

        //   IRealTimeCommunicationPort Implementation

        /// <summary>
        /// Broadcasts a message to all connected clients
        /// </summary>
        /// <param name="request">The broadcast request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> BroadcastMessageAsync(BroadcastMessageRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Result.WithFailure("Broadcast request cannot be null");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            try
            {
                // Enhanced IndTrace pattern: Rate-limited logging with proper context
                _logger.LogRateLimited("Broadcasting message to {Target}: {MessageType}",
                    request.Target, request.MessageType);

                // IndTrace pattern: Exception-free error handling with Result<T>
                // Cast to appropriate hub context based on target
                switch (request.Target)
                {
                    case CommunicationTarget.System:
                        await _systemHub.Clients.All.SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    case CommunicationTarget.Agent:
                        await _agentHub.Clients.All.SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    case CommunicationTarget.Task:
                        await _taskHub.Clients.All.SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    case CommunicationTarget.Document:
                        await _documentHub.Clients.All.SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    case CommunicationTarget.Economic:
                        await _economicHub.Clients.All.SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    default:
                        return Result.WithFailure($"Unknown target: {request.Target}");
                }

                _logger.LogRateLimited("Successfully broadcast message to {Target}: {MessageType}",
                    request.Target, request.MessageType);

                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("Broadcast operation was canceled for {Target}: {MessageType}",
                    request.Target, request.MessageType);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast message to {Target}: {MessageType}",
                    request.Target, request.MessageType);
                return Result.WithFailure($"Message broadcast failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a message to a specific group of clients
        /// </summary>
        /// <param name="request">The group message request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> SendToGroupAsync(GroupMessageRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Result.WithFailure("Group message request cannot be null");
            }

            if (string.IsNullOrWhiteSpace(request.GroupName))
            {
                return Result.WithFailure("Group name cannot be null or empty");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            try
            {
                _logger.LogRateLimited("Sending message to group {GroupName} on {Target}: {MessageType}",
                    request.GroupName, request.Target, request.MessageType);

                // Direct hub context usage based on target
                switch (request.Target)
                {
                    case CommunicationTarget.System:
                        await _systemHub.Clients.Group(request.GroupName).SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    case CommunicationTarget.Agent:
                        await _agentHub.Clients.Group(request.GroupName).SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    case CommunicationTarget.Task:
                        await _taskHub.Clients.Group(request.GroupName).SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    case CommunicationTarget.Document:
                        await _documentHub.Clients.Group(request.GroupName).SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    case CommunicationTarget.Economic:
                        await _economicHub.Clients.Group(request.GroupName).SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    default:
                        return Result.WithFailure($"Unknown target: {request.Target}");
                }

                _logger.LogRateLimited("Successfully sent message to group {GroupName} on {Target}: {MessageType}",
                    request.GroupName, request.Target, request.MessageType);

                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("Group message operation was canceled for {GroupName} on {Target}: {MessageType}",
                    request.GroupName, request.Target, request.MessageType);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to group {GroupName} on {Target}: {MessageType}",
                    request.GroupName, request.Target, request.MessageType);
                return Result.WithFailure($"Group message failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a message to a specific user
        /// </summary>
        /// <param name="request">The user message request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> SendToUserAsync(UserMessageRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Result.WithFailure("User message request cannot be null");
            }

            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return Result.WithFailure("User ID cannot be null or empty");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            try
            {
                _logger.LogRateLimited("Sending message to user {UserId} on {Target}: {MessageType}",
                    request.UserId, request.Target, request.MessageType);

                // Direct hub context usage based on target
                switch (request.Target)
                {
                    case CommunicationTarget.System:
                        await _systemHub.Clients.User(request.UserId).SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    case CommunicationTarget.Agent:
                        await _agentHub.Clients.User(request.UserId).SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    case CommunicationTarget.Task:
                        await _taskHub.Clients.User(request.UserId).SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    case CommunicationTarget.Document:
                        await _documentHub.Clients.User(request.UserId).SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    case CommunicationTarget.Economic:
                        await _economicHub.Clients.User(request.UserId).SendAsync(request.MessageType, request.Data, cancellationToken);
                        break;

                    default:
                        return Result.WithFailure($"Unknown target: {request.Target}");
                }

                _logger.LogRateLimited("Successfully sent message to user {UserId} on {Target}: {MessageType}",
                    request.UserId, request.Target, request.MessageType);

                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("User message operation was canceled for {UserId} on {Target}: {MessageType}",
                    request.UserId, request.Target, request.MessageType);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to user {UserId} on {Target}: {MessageType}",
                    request.UserId, request.Target, request.MessageType);
                return Result.WithFailure($"User message failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a connection to a group
        /// </summary>
        /// <param name="request">The add to group request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> AddToGroupAsync(AddToGroupRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Result.WithFailure("Add to group request cannot be null");
            }

            if (string.IsNullOrWhiteSpace(request.ConnectionId))
            {
                return Result.WithFailure("Connection ID cannot be null or empty");
            }

            if (string.IsNullOrWhiteSpace(request.GroupName))
            {
                return Result.WithFailure("Group name cannot be null or empty");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            // IndTrace pattern: Thread-safe connection management
            await _connectionSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogRateLimited("Adding connection {ConnectionId} to group {GroupName} on {Target}",
                    request.ConnectionId, request.GroupName, request.Target);

                // Direct hub context usage based on target
                switch (request.Target)
                {
                    case CommunicationTarget.System:
                        await _systemHub.Groups.AddToGroupAsync(request.ConnectionId, request.GroupName, cancellationToken);
                        break;

                    case CommunicationTarget.Agent:
                        await _agentHub.Groups.AddToGroupAsync(request.ConnectionId, request.GroupName, cancellationToken);
                        break;

                    case CommunicationTarget.Task:
                        await _taskHub.Groups.AddToGroupAsync(request.ConnectionId, request.GroupName, cancellationToken);
                        break;

                    case CommunicationTarget.Document:
                        await _documentHub.Groups.AddToGroupAsync(request.ConnectionId, request.GroupName, cancellationToken);
                        break;

                    case CommunicationTarget.Economic:
                        await _economicHub.Groups.AddToGroupAsync(request.ConnectionId, request.GroupName, cancellationToken);
                        break;

                    default:
                        return Result.WithFailure($"Unknown target: {request.Target}");
                }

                _logger.LogRateLimited("Successfully added connection {ConnectionId} to group {GroupName} on {Target}",
                    request.ConnectionId, request.GroupName, request.Target);

                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("Add to group operation was canceled for {ConnectionId} to {GroupName} on {Target}",
                    request.ConnectionId, request.GroupName, request.Target);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add connection {ConnectionId} to group {GroupName} on {Target}",
                    request.ConnectionId, request.GroupName, request.Target);
                return Result.WithFailure($"Add to group failed: {ex.Message}");
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Removes a connection from a group
        /// </summary>
        /// <param name="request">The remove from group request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> RemoveFromGroupAsync(RemoveFromGroupRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Result.WithFailure("Remove from group request cannot be null");
            }

            if (string.IsNullOrWhiteSpace(request.ConnectionId))
            {
                return Result.WithFailure("Connection ID cannot be null or empty");
            }

            if (string.IsNullOrWhiteSpace(request.GroupName))
            {
                return Result.WithFailure("Group name cannot be null or empty");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            // IndTrace pattern: Thread-safe connection management
            await _connectionSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogRateLimited("Removing connection {ConnectionId} from group {GroupName} on {Target}",
                    request.ConnectionId, request.GroupName, request.Target);

                // Direct hub context usage based on target
                switch (request.Target)
                {
                    case CommunicationTarget.System:
                        await _systemHub.Groups.RemoveFromGroupAsync(request.ConnectionId, request.GroupName, cancellationToken);
                        break;

                    case CommunicationTarget.Agent:
                        await _agentHub.Groups.RemoveFromGroupAsync(request.ConnectionId, request.GroupName, cancellationToken);
                        break;

                    case CommunicationTarget.Task:
                        await _taskHub.Groups.RemoveFromGroupAsync(request.ConnectionId, request.GroupName, cancellationToken);
                        break;

                    case CommunicationTarget.Document:
                        await _documentHub.Groups.RemoveFromGroupAsync(request.ConnectionId, request.GroupName, cancellationToken);
                        break;

                    case CommunicationTarget.Economic:
                        await _economicHub.Groups.RemoveFromGroupAsync(request.ConnectionId, request.GroupName, cancellationToken);
                        break;

                    default:
                        return Result.WithFailure($"Unknown target: {request.Target}");
                }

                _logger.LogRateLimited("Successfully removed connection {ConnectionId} from group {GroupName} on {Target}",
                    request.ConnectionId, request.GroupName, request.Target);

                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("Remove from group operation was canceled for {ConnectionId} from {GroupName} on {Target}",
                    request.ConnectionId, request.GroupName, request.Target);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove connection {ConnectionId} from group {GroupName} on {Target}",
                    request.ConnectionId, request.GroupName, request.Target);
                return Result.WithFailure($"Remove from group failed: {ex.Message}");
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        //   IRealTimeCommunicationPort Implementation

        //   IEventBroadcastingPort Implementation

        /// <summary>
        /// Broadcasts a system event to all clients
        /// </summary>
        /// <param name="systemEvent">The system event</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> BroadcastSystemEventAsync(SystemEvent systemEvent, CancellationToken cancellationToken = default)
        {
            if (systemEvent is null)
            {
                return Result.WithFailure("System event cannot be null");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            try
            {
                _logger.LogRateLimited("Broadcasting system event: {EventType}", systemEvent.EventType);

                await _systemHub.Clients.All.SendAsync("SystemEvent", systemEvent, cancellationToken);

                _logger.LogRateLimited("Successfully broadcast system event: {EventType}", systemEvent.EventType);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("System event broadcast was canceled: {EventType}", systemEvent.EventType);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast system event: {EventType}", systemEvent.EventType);
                return Result.WithFailure($"System event broadcast failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcasts an agent event to all clients
        /// </summary>
        /// <param name="agentEvent">The agent event</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> BroadcastAgentEventAsync(AgentEvent agentEvent, CancellationToken cancellationToken = default)
        {
            if (agentEvent is null)
            {
                return Result.WithFailure("Agent event cannot be null");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            try
            {
                _logger.LogRateLimited("Broadcasting agent event: {EventType} for agent {AgentId}",
                    agentEvent.EventType, agentEvent.AgentId);

                await _agentHub.Clients.All.SendAsync("AgentEvent", agentEvent, cancellationToken);

                _logger.LogRateLimited("Successfully broadcast agent event: {EventType} for agent {AgentId}",
                    agentEvent.EventType, agentEvent.AgentId);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("Agent event broadcast was canceled: {EventType} for agent {AgentId}",
                    agentEvent.EventType, agentEvent.AgentId);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast agent event: {EventType} for agent {AgentId}",
                    agentEvent.EventType, agentEvent.AgentId);
                return Result.WithFailure($"Agent event broadcast failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcasts a task event to all clients
        /// </summary>
        /// <param name="taskEvent">The task event</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> BroadcastTaskEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default)
        {
            if (taskEvent is null)
            {
                return Result.WithFailure("Task event cannot be null");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            try
            {
                _logger.LogRateLimited("Broadcasting task event: {EventType} for task {TaskId}",
                    taskEvent.EventType, taskEvent.TaskId);

                await _taskHub.Clients.All.SendAsync("TaskEvent", taskEvent, cancellationToken);

                _logger.LogRateLimited("Successfully broadcast task event: {EventType} for task {TaskId}",
                    taskEvent.EventType, taskEvent.TaskId);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("Task event broadcast was canceled: {EventType} for task {TaskId}",
                    taskEvent.EventType, taskEvent.TaskId);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast task event: {EventType} for task {TaskId}",
                    taskEvent.EventType, taskEvent.TaskId);
                return Result.WithFailure($"Task event broadcast failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcasts a document event to all clients
        /// </summary>
        /// <param name="documentEvent">The document event</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> BroadcastDocumentEventAsync(DocumentEvent documentEvent, CancellationToken cancellationToken = default)
        {
            if (documentEvent is null)
            {
                return Result.WithFailure("Document event cannot be null");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            try
            {
                _logger.LogRateLimited("Broadcasting document event: {EventType} for document {DocumentId}",
                    documentEvent.EventType, documentEvent.DocumentId);

                await _documentHub.Clients.All.SendAsync("DocumentEvent", documentEvent, cancellationToken);

                _logger.LogRateLimited("Successfully broadcast document event: {EventType} for document {DocumentId}",
                    documentEvent.EventType, documentEvent.DocumentId);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("Document event broadcast was canceled: {EventType} for document {DocumentId}",
                    documentEvent.EventType, documentEvent.DocumentId);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast document event: {EventType} for document {DocumentId}",
                    documentEvent.EventType, documentEvent.DocumentId);
                return Result.WithFailure($"Document event broadcast failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcasts an economic event to all clients
        /// </summary>
        /// <param name="economicEvent">The economic event</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> BroadcastEconomicEventAsync(EconomicEvent economicEvent, CancellationToken cancellationToken = default)
        {
            if (economicEvent is null)
            {
                return Result.WithFailure("Economic event cannot be null");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            try
            {
                _logger.LogRateLimited("Broadcasting economic event: {EventType}", economicEvent.EventType);

                await _economicHub.Clients.All.SendAsync("EconomicEvent", economicEvent, cancellationToken);

                _logger.LogRateLimited("Successfully broadcast economic event: {EventType}", economicEvent.EventType);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("Economic event broadcast was canceled: {EventType}", economicEvent.EventType);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast economic event: {EventType}", economicEvent.EventType);
                return Result.WithFailure($"Economic event broadcast failed: {ex.Message}");
            }
        }

        //   IEventBroadcastingPort Implementation

        //   INotificationPort Implementation

        /// <summary>
        /// Sends a notification to clients
        /// </summary>
        /// <param name="request">The notification request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> SendNotificationAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Result.WithFailure("Notification request cannot be null");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            try
            {
                _logger.LogRateLimited("Sending notification: {NotificationType} to {Target}",
                    request.NotificationType, request.Target);

                // Direct hub context usage based on target
                switch (request.Target)
                {
                    case CommunicationTarget.System:
                        await _systemHub.Clients.All.SendAsync("Notification", request, cancellationToken);
                        break;

                    case CommunicationTarget.Agent:
                        await _agentHub.Clients.All.SendAsync("Notification", request, cancellationToken);
                        break;

                    case CommunicationTarget.Task:
                        await _taskHub.Clients.All.SendAsync("Notification", request, cancellationToken);
                        break;

                    case CommunicationTarget.Document:
                        await _documentHub.Clients.All.SendAsync("Notification", request, cancellationToken);
                        break;

                    case CommunicationTarget.Economic:
                        await _economicHub.Clients.All.SendAsync("Notification", request, cancellationToken);
                        break;

                    default:
                        return Result.WithFailure($"Unknown target: {request.Target}");
                }

                _logger.LogRateLimited("Successfully sent notification: {NotificationType} to {Target}",
                    request.NotificationType, request.Target);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("Notification operation was canceled: {NotificationType} to {Target}",
                    request.NotificationType, request.Target);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification: {NotificationType} to {Target}",
                    request.NotificationType, request.Target);
                return Result.WithFailure($"Notification failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends an alert to clients
        /// </summary>
        /// <param name="request">The alert request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> SendAlertAsync(AlertRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Result.WithFailure("Alert request cannot be null");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            try
            {
                // Use appropriate log level based on alert severity
                var logLevel = request.Severity switch
                {
                    CommunicationAlertSeverity.Critical => LogLevel.Critical,
                    CommunicationAlertSeverity.Error => LogLevel.Error,
                    CommunicationAlertSeverity.Warning => LogLevel.Warning,
                    _ => LogLevel.Information
                };

                _logger.Log(logLevel, "Sending {Severity} alert: {AlertType} - {Message} to {Target}",
                    request.Severity, request.AlertType, request.Message, request.Target);

                // Direct hub context usage based on target
                switch (request.Target)
                {
                    case CommunicationTarget.System:
                        await _systemHub.Clients.All.SendAsync("Alert", request, cancellationToken);
                        break;

                    case CommunicationTarget.Agent:
                        await _agentHub.Clients.All.SendAsync("Alert", request, cancellationToken);
                        break;

                    case CommunicationTarget.Task:
                        await _taskHub.Clients.All.SendAsync("Alert", request, cancellationToken);
                        break;

                    case CommunicationTarget.Document:
                        await _documentHub.Clients.All.SendAsync("Alert", request, cancellationToken);
                        break;

                    case CommunicationTarget.Economic:
                        await _economicHub.Clients.All.SendAsync("Alert", request, cancellationToken);
                        break;

                    default:
                        return Result.WithFailure($"Unknown target: {request.Target}");
                }

                _logger.LogRateLimited("Successfully sent {Severity} alert: {AlertType} to {Target}",
                    request.Severity, request.AlertType, request.Target);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("Alert operation was canceled: {AlertType} to {Target}",
                    request.AlertType, request.Target);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send alert: {AlertType} to {Target}",
                    request.AlertType, request.Target);
                return Result.WithFailure($"Alert failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a status update to clients
        /// </summary>
        /// <param name="request">The status update request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> SendStatusUpdateAsync(StatusUpdateRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Result.WithFailure("Status update request cannot be null");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            try
            {
                _logger.LogRateLimited("Sending status update: {Status} for {EntityType} {EntityId} to {Target}",
                    request.Status, request.EntityType, request.EntityId, request.Target);

                // Direct hub context usage based on target
                switch (request.Target)
                {
                    case CommunicationTarget.System:
                        await _systemHub.Clients.All.SendAsync("StatusUpdate", request, cancellationToken);
                        break;

                    case CommunicationTarget.Agent:
                        await _agentHub.Clients.All.SendAsync("StatusUpdate", request, cancellationToken);
                        break;

                    case CommunicationTarget.Task:
                        await _taskHub.Clients.All.SendAsync("StatusUpdate", request, cancellationToken);
                        break;

                    case CommunicationTarget.Document:
                        await _documentHub.Clients.All.SendAsync("StatusUpdate", request, cancellationToken);
                        break;

                    case CommunicationTarget.Economic:
                        await _economicHub.Clients.All.SendAsync("StatusUpdate", request, cancellationToken);
                        break;

                    default:
                        return Result.WithFailure($"Unknown target: {request.Target}");
                }

                _logger.LogRateLimited("Successfully sent status update: {Status} for {EntityType} {EntityId} to {Target}",
                    request.Status, request.EntityType, request.EntityId, request.Target);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("Status update operation was canceled: {Status} for {EntityType} {EntityId} to {Target}",
                    request.Status, request.EntityType, request.EntityId, request.Target);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send status update: {Status} for {EntityType} {EntityId} to {Target}",
                    request.Status, request.EntityType, request.EntityId, request.Target);
                return Result.WithFailure($"Status update failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a progress update to clients
        /// </summary>
        /// <param name="request">The progress update request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result> SendProgressUpdateAsync(ProgressUpdateRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Result.WithFailure("Progress update request cannot be null");
            }

            if (request.Progress < 0 || request.Progress > 100)
            {
                return Result.WithFailure("Progress must be between 0 and 100");
            }

            if (_disposed)
            {
                return Result.WithFailure("Adapter has been disposed");
            }

            try
            {
                _logger.LogRateLimited("Sending progress update: {Progress}% for {EntityType} {EntityId} to {Target}",
                    request.Progress, request.EntityType, request.EntityId, request.Target);

                // Direct hub context usage based on target
                switch (request.Target)
                {
                    case CommunicationTarget.System:
                        await _systemHub.Clients.All.SendAsync("ProgressUpdate", request, cancellationToken);
                        break;

                    case CommunicationTarget.Agent:
                        await _agentHub.Clients.All.SendAsync("ProgressUpdate", request, cancellationToken);
                        break;

                    case CommunicationTarget.Task:
                        await _taskHub.Clients.All.SendAsync("ProgressUpdate", request, cancellationToken);
                        break;

                    case CommunicationTarget.Document:
                        await _documentHub.Clients.All.SendAsync("ProgressUpdate", request, cancellationToken);
                        break;

                    case CommunicationTarget.Economic:
                        await _economicHub.Clients.All.SendAsync("ProgressUpdate", request, cancellationToken);
                        break;

                    default:
                        return Result.WithFailure($"Unknown target: {request.Target}");
                }

                _logger.LogRateLimited("Successfully sent progress update: {Progress}% for {EntityType} {EntityId} to {Target}",
                    request.Progress, request.EntityType, request.EntityId, request.Target);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _logger.LogRateLimited("Progress update operation was canceled: {Progress}% for {EntityType} {EntityId} to {Target}",
                    request.Progress, request.EntityType, request.EntityId, request.Target);
                return Result.WithFailure("Operation was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send progress update: {Progress}% for {EntityType} {EntityId} to {Target}",
                    request.Progress, request.EntityType, request.EntityId, request.Target);
                return Result.WithFailure($"Progress update failed: {ex.Message}");
            }
        }

        //   INotificationPort Implementation

        //   Private Helper Methods

        // IndTrace pattern: Helper methods removed in favor of direct hub context usage
        // This improves type safety and eliminates casting issues

        //   Private Helper Methods

        //   IDisposable and IAsyncDisposable Implementation

        /// <summary>
        /// Synchronously disposes the adapter and releases resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                // IndTrace pattern: Proper synchronous disposal with timeout
                if (_connectionSemaphore.Wait(TimeSpan.FromSeconds(5)))
                {
                    _connectionSemaphore.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SignalRAdapter synchronous disposal");
            }

            _logger.LogInformation("SignalRAdapter disposed synchronously");
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the adapter and releases resources
        /// </summary>
        /// <returns>A task representing the asynchronous dispose operation</returns>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                // IndTrace pattern: Proper async disposal with timeout
                await _connectionSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
                _connectionSemaphore.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SignalRAdapter async disposal");
            }

            _logger.LogInformation("SignalRAdapter disposed asynchronously");
            GC.SuppressFinalize(this);
        }

        //   IDisposable and IAsyncDisposable Implementation
    }
}
