using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Hubs;

/// <summary>
/// Base class for SignalR hubs following Hexagonal Architecture and Railway-Oriented Programming patterns.
/// Provides generic, type-safe hub functionality with error handling via Result&lt;T&gt; pattern.
/// </summary>
/// <typeparam name="T">The type of data transmitted through the hub.</typeparam>
public abstract class ExxerHub<T> : Hub, IExxerHub<T>
{
    private readonly ILogger<ExxerHub<T>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExxerHub{T}"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    protected ExxerHub(ILogger<ExxerHub<T>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public virtual async Task<Result> SendToAllAsync(T data, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled before sending to all clients");
            return ResultExtensions.Cancelled();
        }

        try
        {
            await Clients.All.SendAsync("ReceiveMessage", data, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Successfully sent data to all clients");
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Operation cancelled while sending to all clients");
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending data to all clients");
            return Result.WithFailure($"Failed to send data to all clients: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result> SendToClientAsync(string connectionId, T data, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled before sending to client");
            return ResultExtensions.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return Result.WithFailure("Connection ID cannot be null or empty");
        }

        try
        {
            await Clients.Client(connectionId).SendAsync("ReceiveMessage", data, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Successfully sent data to client {ConnectionId}", connectionId);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Operation cancelled while sending to client {ConnectionId}", connectionId);
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending data to client {ConnectionId}", connectionId);
            return Result.WithFailure($"Failed to send data to client {connectionId}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result> SendToGroupAsync(string groupName, T data, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled before sending to group");
            return ResultExtensions.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(groupName))
        {
            return Result.WithFailure("Group name cannot be null or empty");
        }

        try
        {
            await Clients.Group(groupName).SendAsync("ReceiveMessage", data, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Successfully sent data to group {GroupName}", groupName);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Operation cancelled while sending to group {GroupName}", groupName);
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending data to group {GroupName}", groupName);
            return Result.WithFailure($"Failed to send data to group {groupName}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public virtual Task<Result<int>> GetConnectionCountAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled before getting connection count");
            return Task.FromResult(ResultExtensions.Cancelled<int>());
        }

        try
        {
            // Note: SignalR doesn't provide direct connection count access
            // This is a placeholder - implementations should track connections via OnConnectedAsync/OnDisconnectedAsync
            _logger.LogWarning("GetConnectionCountAsync not fully implemented - connection tracking required");
            return Task.FromResult(Result<int>.WithFailure("Connection count tracking not implemented"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connection count");
            return Task.FromResult(Result<int>.WithFailure($"Failed to get connection count: {ex.Message}"));
        }
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnection, if any.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }
}

