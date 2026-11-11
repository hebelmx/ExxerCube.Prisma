namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Identifies the actor that acquired or released a SIARA session, for per-document non-repudiation
/// and the per-process access audit (ADR-010 P2).
/// </summary>
/// <remarks>
/// <para>
/// This record carries <strong>no credentials</strong> — it is a trustworthy identity token produced by
/// ISiaraActorIdentityProvider, not a secret. ActorId is a stable, deployment-configured identifier
/// (service account name, user principal, or process identity) that can be recorded in audit trails and
/// surfaced in compliance reports without leaking anything sensitive.
/// </para>
/// <para>
/// SiaraSession.AcquiredBy is of this type, making the acquiring actor mandatory and tamper-evident:
/// a session cannot be created without a resolved, policy-verified actor identity.
/// </para>
/// </remarks>
public sealed record SiaraActor
{
    /// <summary>
    /// Gets the stable, deployment-configured identifier for this actor (for example, a service account
    /// name, a user principal name, or a process identity). Never a credential. Recorded in audit trails.
    /// </summary>
    public required string ActorId { get; init; }

    /// <summary>
    /// Gets the kind of actor — user (human operator) or service account (unattended process). Used to
    /// route alerting and to shape the compliance trail.
    /// </summary>
    public required Enum.SiaraActorType ActorType { get; init; }

    /// <summary>
    /// Gets an optional human-readable label for the actor (for example, a display name or team name).
    /// Informational only; the canonical identity for audit purposes is ActorId.
    /// </summary>
    public string? DisplayName { get; init; }
}
