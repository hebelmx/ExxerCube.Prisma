// <copyright file="HubContextDomainEventBroadcaster.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Web.UI.Hubs;
using IndFusion.Ember.Abstractions.Hubs;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.AspNetCore.SignalR;

namespace ExxerCube.Prisma.Web.UI.Services;

/// <summary>
/// <see cref="IExxerHub{T}"/> transport for <see cref="DomainEvent"/> that broadcasts over the
/// <see cref="ProcessingHub"/> using <see cref="IHubContext{THub}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This adapter exists because <see cref="ExxerHub{T}"/> derives from <see cref="Hub"/>, whose
/// <c>Clients</c> property is only populated by the SignalR runtime during an <em>inbound</em> hub
/// invocation. A hub resolved from DI to send <em>outbound</em> (as <see cref="SignalREventBroadcaster"/>
/// does) has a null <c>Clients</c>, so its <c>SendToAllAsync</c> silently fails — which is exactly what the
/// previous <c>AddScoped&lt;IExxerHub&lt;DomainEvent&gt;, ProcessingHub&gt;()</c> registration produced (no
/// UI live-update ever reached the browser). The hub context is the supported way to broadcast from outside
/// the hub; this mirrors Orion's <c>SignalRIngestionBroadcaster</c>.
/// </para>
/// <para>
/// Events are sent on the Ember <c>"ReceiveMessage"</c> wire method. The Dashboard / SLA Razor pages handle
/// it with <c>hubConnection.On&lt;object&gt;("ReceiveMessage", _ =&gt; InvokeAsync(reload))</c> (FU1).
/// Railway-Oriented: every method returns <see cref="Result"/> and never throws for transport outcomes — a
/// failed UI broadcast must never break the pipeline.
/// </para>
/// </remarks>
public sealed class HubContextDomainEventBroadcaster : IExxerHub<DomainEvent>
{
    private const string ReceiveMessage = "ReceiveMessage";

    private readonly IHubContext<ProcessingHub> _hubContext;
    private readonly ILogger<HubContextDomainEventBroadcaster> _logger;

    /// <summary>Initializes a new instance of the <see cref="HubContextDomainEventBroadcaster"/> class.</summary>
    /// <param name="hubContext">The SignalR hub context used to broadcast from outside the hub.</param>
    /// <param name="logger">The logger instance.</param>
    public HubContextDomainEventBroadcaster(
        IHubContext<ProcessingHub> hubContext,
        ILogger<HubContextDomainEventBroadcaster> logger)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        ArgumentNullException.ThrowIfNull(logger);
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> SendToAllAsync(DomainEvent data, CancellationToken cancellationToken = default)
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
            _logger.LogDebug(
                "Broadcast domain event {EventType} ({EventId}) to all UI clients via ProcessingHub",
                data.EventType,
                data.EventId);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast domain event {EventType} ({EventId})", data?.EventType, data?.EventId);
            return Result.WithFailure($"Failed to broadcast event: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> SendToClientAsync(string connectionId, DomainEvent data, CancellationToken cancellationToken = default)
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
            _logger.LogError(ex, "Failed to send domain event to client {ConnectionId}", connectionId);
            return Result.WithFailure($"Failed to send event to client {connectionId}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> SendToGroupAsync(string groupName, DomainEvent data, CancellationToken cancellationToken = default)
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
            _logger.LogError(ex, "Failed to send domain event to group {GroupName}", groupName);
            return Result.WithFailure($"Failed to send event to group {groupName}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<Result<int>> GetConnectionCountAsync(CancellationToken cancellationToken = default) =>
        // SignalR exposes no direct connection count without OnConnected/OnDisconnected bookkeeping, which the
        // UI observability edge does not need. Matches ExxerHub<T>'s contract (and SignalRIngestionBroadcaster).
        Task.FromResult(cancellationToken.IsCancellationRequested
            ? ResultExtensions.Cancelled<int>()
            : Result<int>.WithFailure("Connection count tracking not implemented"));
}
