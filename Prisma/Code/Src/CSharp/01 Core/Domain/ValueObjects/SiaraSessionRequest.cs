namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Describes what a caller asks for when acquiring an authenticated SIARA session.
/// </summary>
/// <remarks>
/// The same request shape serves both strategies; each reads the fields relevant to it.
/// It carries <strong>no</strong> raw credentials — neither strategy accepts a username or
/// password (ADR-010).
/// </remarks>
public sealed record SiaraSessionRequest
{
    /// <summary>
    /// Gets the externally-provided authenticated context for
    /// <see cref="Enum.SiaraAuthMode.SessionPassthrough"/>: a CDP/WebSocket connect endpoint or an
    /// imported storage-state reference. Ignored by the interactive-login strategy.
    /// </summary>
    public string? ExistingContextEndpoint { get; init; }

    /// <summary>
    /// Gets how long to await a one-time human login for
    /// <see cref="Enum.SiaraAuthMode.InteractiveLogin"/>. Ignored by the passthrough strategy.
    /// </summary>
    public TimeSpan? MaxWaitForHuman { get; init; }

    /// <summary>
    /// Gets an optional caller-supplied correlation hint. The audited acquiring identity is the
    /// trustworthy actor resolved by ISiaraActorIdentityProvider, NOT this field (ADR-010 P2).
    /// </summary>
    public string? RequestedBy { get; init; }
}
