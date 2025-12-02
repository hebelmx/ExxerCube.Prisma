namespace ExxerCube.Prisma.Infrastructure.Events;

/// <summary>
/// In-memory event bus implementation for pub/sub messaging within a single process.
/// </summary>
/// <remarks>
/// ITDD Implementation Notes:
/// - Implements both IEventPublisher and IEventSubscriber (singleton pattern)
/// - Thread-safe for concurrent subscription and publishing
/// - Defensive: NEVER throws on handler failures (logs and continues)
/// - Handlers invoked in registration order
/// - Correlation IDs preserved through logging
/// </remarks>
public sealed class InMemoryEventBus : IEventPublisher, IEventSubscriber
{
    private readonly ILogger<InMemoryEventBus> _logger;
    private readonly Dictionary<string, List<object>> _handlers = new();
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryEventBus"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void Subscribe<TEvent>(string eventName, IEventHandler<TEvent> handler)
        where TEvent : notnull
    {
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("Event name cannot be null or whitespace.", nameof(eventName));

        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        lock (_lock)
        {
            if (!_handlers.ContainsKey(eventName))
            {
                _handlers[eventName] = new List<object>();
            }

            _handlers[eventName].Add(handler);
            _logger.LogInformation(
                "Handler {HandlerType} subscribed to event {EventName}",
                handler.GetType().Name,
                eventName);
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(
        string eventName,
        TEvent payload,
        Guid correlationId,
        CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            _logger.LogWarning(
                "Cannot publish event with null/empty name. CorrelationId: {CorrelationId}",
                correlationId);
            return; // DEFENSIVE - don't throw
        }

        if (payload is null)
        {
            _logger.LogWarning(
                "Cannot publish null payload for event {EventName}. CorrelationId: {CorrelationId}",
                eventName,
                correlationId);
            return; // DEFENSIVE - don't throw
        }

        List<object>? handlersCopy;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(eventName, out var handlers))
            {
                _logger.LogDebug(
                    "No handlers registered for event {EventName}. CorrelationId: {CorrelationId}",
                    eventName,
                    correlationId);
                return; // DEFENSIVE - no throw, just log
            }

            // Copy to avoid holding lock during async handler invocations
            handlersCopy = new List<object>(handlers);
        }

        _logger.LogInformation(
            "Publishing event {EventName} to {HandlerCount} handler(s). CorrelationId: {CorrelationId}",
            eventName,
            handlersCopy.Count,
            correlationId);

        foreach (var handler in handlersCopy.Cast<IEventHandler<TEvent>>())
        {
            try
            {
                await handler.HandleAsync(eventName, payload, correlationId, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogDebug(
                    "Handler {HandlerType} successfully processed event {EventName}. CorrelationId: {CorrelationId}",
                    handler.GetType().Name,
                    eventName,
                    correlationId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Handler {HandlerType} cancelled for event {EventName}. CorrelationId: {CorrelationId}",
                    handler.GetType().Name,
                    eventName,
                    correlationId);
                // Don't process further handlers if cancellation requested
                break;
            }
            catch (Exception ex)
            {
                // DEFENSIVE - log and continue, NEVER crash the event bus
                _logger.LogError(
                    ex,
                    "Handler {HandlerType} failed for event {EventName}. CorrelationId: {CorrelationId}. Error: {ErrorMessage}",
                    handler.GetType().Name,
                    eventName,
                    correlationId,
                    ex.Message);
                // Continue to next handler
            }
        }

        _logger.LogInformation(
            "Event {EventName} published successfully. CorrelationId: {CorrelationId}",
            eventName,
            correlationId);
    }

    /// <summary>
    ///  Publishes a domain event to all subscribed handlers.
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <param name="domainEvent"></param>
    /// <exception cref="NotImplementedException"></exception>

    public void Publish<TEvent>(TEvent domainEvent) where TEvent : DomainEvent
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets a stream of domain events of the specified type.
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public IObservable<TEvent> GetEventStream<TEvent>() where TEvent : DomainEvent
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets a stream of all domain events.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public IObservable<DomainEvent> GetAllEventsStream()
    {
        throw new NotImplementedException();
    }
}