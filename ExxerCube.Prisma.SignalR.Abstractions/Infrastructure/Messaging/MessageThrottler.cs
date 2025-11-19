using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.SignalR.Abstractions.Infrastructure.Messaging;

/// <summary>
/// Throttles message sending to prevent UI overload.
/// </summary>
/// <typeparam name="T">The type of messages to throttle.</typeparam>
public class MessageThrottler<T> : IDisposable
{
    private readonly ILogger<MessageThrottler<T>> _logger;
    private readonly TimeSpan _throttleInterval;
    private DateTime _lastSendTime;
    private T? _latestMessage;
    private readonly SemaphoreSlim _lock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageThrottler{T}"/> class.
    /// </summary>
    /// <param name="throttleInterval">The minimum time interval between sends.</param>
    /// <param name="logger">The logger instance.</param>
    public MessageThrottler(TimeSpan throttleInterval, ILogger<MessageThrottler<T>> logger)
    {
        _throttleInterval = throttleInterval;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lastSendTime = DateTime.MinValue;
        _lock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Event raised when a throttled message is ready to be sent.
    /// </summary>
    public event EventHandler<ThrottledMessageEventArgs<T>>? MessageReady;

    /// <summary>
    /// Throttles a message for sending.
    /// </summary>
    /// <param name="message">The message to throttle.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ThrottleAsync(T message, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _latestMessage = message;
            var now = DateTime.UtcNow;
            var timeSinceLastSend = now - _lastSendTime;

            if (timeSinceLastSend >= _throttleInterval)
            {
                await SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Schedule delayed send
                var delay = _throttleInterval - timeSinceLastSend;
                _ = Task.Delay(delay, cancellationToken).ContinueWith(async _ =>
                {
                    await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        if (Equals(_latestMessage, message))
                        {
                            await SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        _lock.Release();
                    }
                }, cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SendMessageAsync(T message, CancellationToken cancellationToken)
    {
        _lastSendTime = DateTime.UtcNow;
        _logger.LogDebug("Sending throttled message");

        var args = new ThrottledMessageEventArgs<T>(message);
        MessageReady?.Invoke(this, args);
    }

    /// <summary>
    /// Disposes the message throttler.
    /// </summary>
    public void Dispose()
    {
        _lock.Dispose();
    }
}

/// <summary>
/// Event arguments for throttled message events.
/// </summary>
/// <typeparam name="T">The type of the message.</typeparam>
public class ThrottledMessageEventArgs<T> : EventArgs
{
    /// <summary>
    /// Gets the throttled message.
    /// </summary>
    public T Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThrottledMessageEventArgs{T}"/> class.
    /// </summary>
    /// <param name="message">The throttled message.</param>
    public ThrottledMessageEventArgs(T message)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }
}

