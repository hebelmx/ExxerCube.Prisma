using System;
using System.Threading;
using System.Threading.Tasks;
using IndFusion.Ember.Abstractions.Hubs;
using IndQuestResults;
using Microsoft.Extensions.Logging;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// Stub implementation of IExxerHub for testing and development.
/// Logs events without actually broadcasting them. Replace with actual SignalR hub in production.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public sealed class StubExxerHub<TEvent> : IExxerHub<TEvent> where TEvent : class
{
    private readonly ILogger<StubExxerHub<TEvent>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubExxerHub{TEvent}"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public StubExxerHub(ILogger<StubExxerHub<TEvent>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<Result> SendToAllAsync(TEvent message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StubExxerHub: Broadcasting event {EventType} to all clients (stub - not actually broadcasting)", typeof(TEvent).Name);
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public Task<Result> SendToGroupAsync(string groupName, TEvent message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StubExxerHub: Broadcasting event {EventType} to group {GroupName} (stub - not actually broadcasting)", typeof(TEvent).Name, groupName);
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public Task<Result> SendToClientAsync(string connectionId, TEvent message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StubExxerHub: Sending event {EventType} to client {ConnectionId} (stub - not actually sending)", typeof(TEvent).Name, connectionId);
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public Task<Result<int>> GetConnectionCountAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StubExxerHub: Getting connection count (stub - returning 0)");
        return Task.FromResult(Result<int>.Success(0));
    }
}
