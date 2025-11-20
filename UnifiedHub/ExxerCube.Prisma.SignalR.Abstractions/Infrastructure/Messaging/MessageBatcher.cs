using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.SignalR.Abstractions.Infrastructure.Messaging;

/// <summary>
/// Batches messages to reduce SignalR traffic and improve performance.
/// </summary>
/// <typeparam name="T">The type of messages to batch.</typeparam>
public class MessageBatcher<T> : IDisposable
{
    private readonly ILogger<MessageBatcher<T>> _logger;
    private readonly int _batchSize;
    private readonly TimeSpan _batchInterval;
    private readonly List<T> _pendingMessages;
    private readonly SemaphoreSlim _lock;
    private Timer? _batchTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageBatcher{T}"/> class.
    /// </summary>
    /// <param name="batchSize">The maximum number of messages per batch.</param>
    /// <param name="batchInterval">The time interval for batching messages.</param>
    /// <param name="logger">The logger instance.</param>
    public MessageBatcher(int batchSize, TimeSpan batchInterval, ILogger<MessageBatcher<T>> logger)
    {
        _batchSize = batchSize > 0 ? batchSize : throw new ArgumentOutOfRangeException(nameof(batchSize));
        _batchInterval = batchInterval;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pendingMessages = new List<T>();
        _lock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Event raised when a batch of messages is ready to be sent.
    /// </summary>
    public event EventHandler<BatchReadyEventArgs<T>>? BatchReady;

    /// <summary>
    /// Adds a message to the batch.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddMessageAsync(T message, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _pendingMessages.Add(message);

            // Start timer if not already started
            if (_batchTimer == null)
            {
                _batchTimer = new Timer(OnBatchTimer, null, _batchInterval, Timeout.InfiniteTimeSpan);
            }

            // Flush if batch size reached
            if (_pendingMessages.Count >= _batchSize)
            {
                await FlushBatchAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Flushes all pending messages immediately.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await FlushBatchAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void OnBatchTimer(object? state)
    {
        // Fire and forget - use Task.Run to handle async properly
        _ = Task.Run(async () =>
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                await FlushBatchAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing batch in timer callback");
            }
            finally
            {
                _lock.Release();
            }
        });
    }

    private async Task FlushBatchAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingMessages.Count == 0)
        {
            return;
        }

        var batch = _pendingMessages.ToList();
        _pendingMessages.Clear();

        _batchTimer?.Dispose();
        _batchTimer = null;

        _logger.LogDebug("Flushing batch of {Count} messages", batch.Count);

        var args = new BatchReadyEventArgs<T>(batch);
        BatchReady?.Invoke(this, args);
    }

    /// <summary>
    /// Disposes the message batcher.
    /// </summary>
    public void Dispose()
    {
        _batchTimer?.Dispose();
        _lock.Dispose();
    }
}

/// <summary>
/// Event arguments for batch ready events.
/// </summary>
/// <typeparam name="T">The type of messages in the batch.</typeparam>
public class BatchReadyEventArgs<T> : EventArgs
{
    /// <summary>
    /// Gets the batch of messages.
    /// </summary>
    public IReadOnlyList<T> Messages { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchReadyEventArgs{T}"/> class.
    /// </summary>
    /// <param name="messages">The batch of messages.</param>
    public BatchReadyEventArgs(IReadOnlyList<T> messages)
    {
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }
}

