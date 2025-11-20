using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Hubs;
using Microsoft.Extensions.Logging;

namespace ExxerAI.RealTimeCommunication.Server.Hubs;

/// <summary>
/// Test implementation of SystemHub for integration testing and usage examples.
/// Demonstrates how to create a hub inheriting from ExxerHub&lt;T&gt;.
/// </summary>
public class TestSystemHub : ExxerHub<SystemMessage>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestSystemHub"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public TestSystemHub(ILogger<TestSystemHub> logger)
        : base(logger)
    {
    }

    /// <summary>
    /// Hub method for broadcasting messages to all clients.
    /// Used by integration tests to simulate client-side message broadcasting.
    /// </summary>
    /// <param name="messageType">The message type identifier.</param>
    /// <param name="data">The message data.</param>
    public async Task BroadcastMessage(string messageType, object data)
    {
        var message = new SystemMessage
        {
            Content = data.ToString() ?? string.Empty,
            MessageType = messageType,
            Timestamp = DateTime.UtcNow
        };
        _ = await base.SendToAllAsync(message, CancellationToken.None);
    }

    /// <summary>
    /// Hub method exposed to SignalR clients for sending messages to all clients.
    /// Wrapper around SendToAllAsync that returns Task for SignalR compatibility.
    /// </summary>
    /// <param name="message">The system message to broadcast.</param>
    public async Task SendMessage(SystemMessage message)
    {
        // Call the base method but ignore the Result return value for SignalR compatibility
        _ = await base.SendToAllAsync(message, CancellationToken.None);
    }

}

/// <summary>
/// System message model for testing and examples.
/// </summary>
public record SystemMessage
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the message timestamp.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the message type.
    /// </summary>
    public string MessageType { get; init; } = string.Empty;
}

