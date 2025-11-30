// <copyright file="SignalREventBroadcaster.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Domain.Events;
using IndFusion.Ember.Abstractions.Hubs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Web.UI.Services;

/// <summary>
/// Background service that subscribes to domain events and broadcasts them using Ember's transport-agnostic abstraction.
/// Provides real-time event streaming to the UI for observability.
/// </summary>
public class SignalREventBroadcaster : BackgroundService
{
    private readonly IEventPublisher _eventPublisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SignalREventBroadcaster> _logger;
    private IDisposable? _subscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalREventBroadcaster"/> class.
    /// </summary>
    /// <param name="eventPublisher">Event publisher to subscribe to.</param>
    /// <param name="scopeFactory">Scope factory for resolving scoped hubs safely.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public SignalREventBroadcaster(
        IEventPublisher eventPublisher,
        IServiceScopeFactory scopeFactory,
        ILogger<SignalREventBroadcaster> logger)
    {
        _eventPublisher = eventPublisher;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes the background service, subscribing to all domain events and broadcasting them.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to signal shutdown.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SignalREventBroadcaster starting - subscribing to domain events");

        _subscription = _eventPublisher
            .GetAllEventsStream()
            .Subscribe(
                onNext: async domainEvent => await BroadcastEventAsync(domainEvent, stoppingToken),
                onError: ex =>
                {
                    _logger.LogError(ex, "Error in event stream subscription");
                    // Defensive Intelligence: Don't throw - log and continue
                },
                onCompleted: () => _logger.LogInformation("Event stream completed"));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Broadcasts a domain event to all clients using Ember's transport-agnostic abstraction.
    /// Uses Railway-Oriented Programming pattern for error handling.
    /// </summary>
    /// <param name="domainEvent">The domain event to broadcast.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task BroadcastEventAsync(DomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // Resolve hub in a scope to avoid singleton depending on scoped services
        using var scope = _scopeFactory.CreateScope();
        var hub = scope.ServiceProvider.GetRequiredService<IExxerHub<DomainEvent>>();

        var result = await hub.SendToAllAsync(domainEvent, cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogDebug(
                "Broadcasted event {EventType} with ID {EventId} via Ember abstraction",
                domainEvent.EventType,
                domainEvent.EventId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to broadcast event {EventType} with ID {EventId}: {Error} - Defensive Intelligence: continuing",
                domainEvent.EventType,
                domainEvent.EventId,
                result.Error);
            // Defensive Intelligence: Don't throw - event broadcasting failure should not break the system
        }
    }

    /// <summary>
    /// Disposes the event stream subscription on shutdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SignalREventBroadcaster stopping - unsubscribing from domain events");
        _subscription?.Dispose();
        return base.StopAsync(cancellationToken);
    }
}
