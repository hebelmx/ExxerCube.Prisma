namespace ExxerCube.Prisma.SignalR.Abstractions.Infrastructure.Connection;

/// <summary>
/// Represents the state of a SignalR connection.
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Connection is disconnected.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Connection is connecting.
    /// </summary>
    Connecting,

    /// <summary>
    /// Connection is connected.
    /// </summary>
    Connected,

    /// <summary>
    /// Connection is reconnecting.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// Connection failed.
    /// </summary>
    Failed
}

