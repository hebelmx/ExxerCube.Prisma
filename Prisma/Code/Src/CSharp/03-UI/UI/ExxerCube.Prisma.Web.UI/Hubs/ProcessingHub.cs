using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndFusion.Ember.Abstractions.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Web.UI.Hubs;

/// <summary>
/// SignalR hub for real-time OCR processing updates and domain event broadcasting.
/// Inherits from ExxerHub to leverage Ember's transport-agnostic abstraction.
/// </summary>
public class ProcessingHub : ExxerHub<DomainEvent>
{
    private readonly ILogger<ProcessingHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingHub"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ProcessingHub(ILogger<ProcessingHub> logger)
        : base(logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sends processing status update to all connected clients.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="status">The processing status.</param>
    /// <param name="progress">The progress percentage (0-100).</param>
    /// <param name="message">The status message.</param>
    public async Task UpdateProcessingStatus(string jobId, string status, int progress, string message)
    {
        _logger.LogInformation("Sending processing status update: JobId={JobId}, Status={Status}, Progress={Progress}%",
            jobId, status, progress);

        await Clients.All.SendAsync("ProcessingStatusUpdated", jobId, status, progress, message);
    }

    /// <summary>
    /// Sends processing completion notification to all connected clients.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="result">The processing result.</param>
    public async Task ProcessingComplete(string jobId, ProcessingResult result)
    {
        _logger.LogInformation("Sending processing completion: JobId={JobId}", jobId);

        await Clients.All.SendAsync("ProcessingComplete", jobId, result);
    }

    /// <summary>
    /// Sends processing error notification to all connected clients.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="error">The error message.</param>
    public async Task ProcessingError(string jobId, string error)
    {
        _logger.LogError("Sending processing error: JobId={JobId}, Error={Error}", jobId, error);

        await Clients.All.SendAsync("ProcessingError", jobId, error);
    }

    /// <summary>
    /// Sends metrics update to all connected clients.
    /// </summary>
    /// <param name="metrics">The processing metrics.</param>
    public async Task UpdateMetrics(object metrics)
    {
        await Clients.All.SendAsync("MetricsUpdated", metrics);
    }

    /// <summary>
    /// Sends SLA status update to all connected clients.
    /// </summary>
    /// <param name="fileId">The file identifier.</param>
    public async Task UpdateSLAStatus(string fileId)
    {
        _logger.LogInformation("Sending SLA status update: FileId={FileId}", fileId);
        await Clients.All.SendAsync("SLAStatusUpdated", fileId);
    }

    /// <summary>
    /// Sends SLA escalation notification to all connected clients.
    /// </summary>
    /// <param name="fileId">The file identifier.</param>
    /// <param name="escalationLevel">The escalation level.</param>
    public async Task NotifySLAEscalation(string fileId, string escalationLevel)
    {
        _logger.LogWarning("Sending SLA escalation notification: FileId={FileId}, Level={EscalationLevel}", fileId, escalationLevel);
        await Clients.All.SendAsync("SLAEscalated", fileId, escalationLevel);
    }

    /// <summary>
    /// Broadcasts a domain event to all connected clients for real-time event streaming.
    /// Uses the inherited SendToAllAsync method from ExxerHub for transport-agnostic broadcasting.
    /// </summary>
    /// <param name="eventType">The type of domain event.</param>
    /// <param name="eventData">The serialized event data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task BroadcastDomainEvent(string eventType, object eventData, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting domain event: {EventType}", eventType);
        await Clients.All.SendAsync("DomainEventOccurred", eventType, eventData, cancellationToken);
    }

    // Note: OnConnectedAsync and OnDisconnectedAsync are inherited from ExxerHub<T>
    // which provides connection tracking and logging
}
