using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// An opaque, credential-free handle to an authenticated SIARA browser session.
/// </summary>
/// <remarks>
/// <para>
/// Both authentication strategies (<see cref="SiaraAuthMode.SessionPassthrough"/> and
/// <see cref="SiaraAuthMode.InteractiveLogin"/>) produce this same artifact: a reusable,
/// refreshable session the scraper consumes. It is what lets the watch loop (MVP-PATH 1.2)
/// run headless for hours off a single acquisition.
/// </para>
/// <para>
/// <strong>Security invariant:</strong> this type contains <em>no</em> username, password, or
/// other raw-credential members — only an opaque storage-state reference. A contract/security
/// test asserts this by reflection (ADR-010 §9). <see cref="StorageStateRef"/> is itself a secret
/// and must never be logged in full.
/// </para>
/// </remarks>
public sealed record SiaraSession
{
    /// <summary>Gets our correlation identifier for this session (for tracing and audit).</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the authentication mode that produced this session.</summary>
    public required SiaraAuthMode Mode { get; init; }

    /// <summary>
    /// Gets an opaque reference to the authenticated storage-state (cookies/local-storage handle).
    /// Never raw credentials, and never to be logged in full — treat as a secret.
    /// </summary>
    public required string StorageStateRef { get; init; }

    /// <summary>
    /// Gets when the session is known to expire, or <see langword="null"/> when the expiry is
    /// unknown (valid until proven invalid by a probe).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets the identity or process that acquired the session, recorded for the per-process
    /// access audit (ties to MVP-PATH A6). Optional.
    /// </summary>
    public string? AcquiredBy { get; init; }
}
