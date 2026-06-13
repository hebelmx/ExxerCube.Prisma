using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Mints and validates short-lived process clearance tokens that bind a <see cref="SiaraActor"/> to a
/// specific pipeline stage and document (MVP-PATH 1.5, A5 DoD).
/// </summary>
/// <remarks>
/// <para>
/// Each cross-process handoff event carries a clearance token. The receiving forwarder calls
/// <see cref="ValidateAsync"/> to confirm the sender's process clearance and the per-document
/// <c>file_id</c> anti-replay binding before forwarding the event into the local pipeline.
/// </para>
/// <para>
/// <strong>Contract:</strong> returns <see cref="Result{T}"/> and never throws for business outcomes;
/// a pre-cancelled token yields a cancelled result; an invalid, expired, or tampered token returns a
/// failure result. Implementations must fail closed — any ambiguity is treated as rejection.
/// </para>
/// </remarks>
public interface IProcessClearanceTokenService
{
    /// <summary>
    /// Mints a short-lived clearance token binding the actor and clearance to one document
    /// (the <c>file_id</c> claim).
    /// </summary>
    /// <param name="actor">The sending process actor identity.</param>
    /// <param name="clearance">The pipeline stage the actor is cleared to initiate.</param>
    /// <param name="fileId">The unique identifier of the document being handed off.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A success result wrapping the signed token string, or a failure result when the token cannot
    /// be minted (for example, when the signing key is not configured).
    /// </returns>
    Task<Result<string>> MintAsync(
        SiaraActor actor,
        ProcessClearance clearance,
        Guid fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the token and extracts the clearance, actor, and file_id claims.
    /// Fails closed on any error — an expired, tampered, or unrecognised token always returns failure.
    /// </summary>
    /// <param name="token">The clearance token string to validate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A success result wrapping the extracted <see cref="ClearanceTokenClaims"/>, or a failure result
    /// when the token is invalid, expired, or does not parse.
    /// </returns>
    Task<Result<ClearanceTokenClaims>> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The verified claims extracted from a validated process clearance token (MVP-PATH 1.5, A5 DoD).
/// </summary>
/// <remarks>
/// Returned by <see cref="IProcessClearanceTokenService.ValidateAsync"/> when the token signature,
/// lifetime, and structural claims all pass. The receiving forwarder uses <see cref="Clearance"/> and
/// <see cref="FileId"/> to enforce the per-stage, per-document rejection policy.
/// </remarks>
public sealed record ClearanceTokenClaims
{
    /// <summary>
    /// Gets the stable, deployment-configured identifier of the sending process actor.
    /// Corresponds to the <c>sub</c> JWT claim.
    /// </summary>
    public required string ActorId { get; init; }

    /// <summary>
    /// Gets the kind of actor — <see cref="Enum.SiaraActorType.User"/> or
    /// <see cref="Enum.SiaraActorType.ServiceAccount"/>.
    /// Corresponds to the <c>actor_type</c> JWT claim.
    /// </summary>
    public required SiaraActorType ActorType { get; init; }

    /// <summary>
    /// Gets the pipeline stage the sending process is cleared to initiate.
    /// Corresponds to the <c>clearance</c> JWT claim.
    /// </summary>
    public required ProcessClearance Clearance { get; init; }

    /// <summary>
    /// Gets the unique identifier of the document this token was minted for.
    /// Corresponds to the <c>file_id</c> JWT claim. The receiving forwarder compares this against the
    /// event's <c>FileId</c> to detect replay or tamper attempts.
    /// </summary>
    public required Guid FileId { get; init; }
}
