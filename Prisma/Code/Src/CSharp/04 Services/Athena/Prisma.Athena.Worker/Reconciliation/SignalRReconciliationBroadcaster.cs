using ExxerCube.Prisma.Domain.Events;
using IndFusion.Ember.Abstractions.Hubs;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.AspNetCore.SignalR;

namespace Prisma.Athena.Worker.Reconciliation;

/// <summary>
/// The real IndFusion.Ember transport for <see cref="ExtractionCompletedEvent"/> (MVP-PATH 1.4 Reconciliator
/// edge). It broadcasts handoff events from the Athena <em>Extractor</em> actor to every connected
/// <em>Reconciliator</em> over SignalR, on the Ember <c>"ReceiveMessage"</c> protocol. Mirrors the 1.3
/// <c>SignalRIngestionBroadcaster</c> on the Downloader → Extractor edge.
/// </summary>
/// <remarks>
/// <para>
/// Outbound sends go through <see cref="IHubContext{THub}"/>, <strong>not</strong> a hub instance:
/// <see cref="ExxerHub{T}"/> derives from <see cref="Hub"/>, whose <c>Clients</c> property is only populated by
/// the SignalR runtime during an inbound invocation — a hub resolved from DI to send outbound has a null
/// <c>Clients</c> and fails. The hub context is the supported way to broadcast from outside the hub.
/// </para>
/// <para>
/// Railway-Oriented: every method returns <see cref="Result"/> and never throws for transport outcomes; a
/// failed broadcast is a failure result the caller logs and continues on. The <c>"ReceiveMessage"</c> method
/// name and payload shape match <see cref="ExxerHub{T}"/> so a plain SignalR client (the Reconciliator)
/// receives with <c>connection.On&lt;ExtractionCompletedEvent&gt;("ReceiveMessage", …)</c>.
/// </para>
/// </remarks>
public sealed class SignalRReconciliationBroadcaster : IExxerHub<ExtractionCompletedEvent>
{
    private const string ReceiveMessage = "ReceiveMessage";

    private readonly IHubContext<ReconciliationHub> _hubContext;
    private readonly ILogger<SignalRReconciliationBroadcaster> _logger;

    /// <summary>Initializes a new instance of the <see cref="SignalRReconciliationBroadcaster"/> class.</summary>
    /// <param name="hubContext">The SignalR hub context used to broadcast from outside the hub.</param>
    /// <param name="logger">The logger instance.</param>
    public SignalRReconciliationBroadcaster(
        IHubContext<ReconciliationHub> hubContext,
        ILogger<SignalRReconciliationBroadcaster> logger)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        ArgumentNullException.ThrowIfNull(logger);

        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> SendToAllAsync(ExtractionCompletedEvent data, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        if (data is null)
        {
            return Result.WithFailure("Event data cannot be null");
        }

        try
        {
            await _hubContext.Clients.All.SendAsync(ReceiveMessage, data, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Broadcast ExtractionCompletedEvent {FileId} (corr {CorrelationId}) to all reconciliation subscribers",
                data.FileId,
                data.CorrelationId);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast ExtractionCompletedEvent {FileId}", data.FileId);
            return Result.WithFailure($"Failed to broadcast event: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> SendToClientAsync(string connectionId, ExtractionCompletedEvent data, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return Result.WithFailure("Connection ID cannot be null or empty");
        }

        if (data is null)
        {
            return Result.WithFailure("Event data cannot be null");
        }

        try
        {
            await _hubContext.Clients.Client(connectionId).SendAsync(ReceiveMessage, data, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ExtractionCompletedEvent to client {ConnectionId}", connectionId);
            return Result.WithFailure($"Failed to send event to client {connectionId}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> SendToGroupAsync(string groupName, ExtractionCompletedEvent data, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(groupName))
        {
            return Result.WithFailure("Group name cannot be null or empty");
        }

        if (data is null)
        {
            return Result.WithFailure("Event data cannot be null");
        }

        try
        {
            await _hubContext.Clients.Group(groupName).SendAsync(ReceiveMessage, data, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ExtractionCompletedEvent to group {GroupName}", groupName);
            return Result.WithFailure($"Failed to send event to group {groupName}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<Result<int>> GetConnectionCountAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(cancellationToken.IsCancellationRequested
            ? ResultExtensions.Cancelled<int>()
            : Result<int>.WithFailure("Connection count tracking not implemented"));
}
