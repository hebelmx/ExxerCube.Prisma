using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Application service for recording human-reviewer disposition decisions against
/// verification findings and statement verdicts.
/// </summary>
/// <remarks>
/// <para>
/// <b>Human-actor invariant:</b> Every disposition must carry a non-empty <c>actor</c>
/// string. VEC never auto-accepts or auto-rejects — there is no code path in this service
/// that calls itself or generates dispositions without an explicit human-supplied actor.
/// Callers that pass a null or whitespace actor receive a typed failure result; no row
/// is written.
/// </para>
/// <para>
/// <b>Append-only invariant:</b> Each call to <see cref="DispositionFindingAsync"/> or
/// <see cref="DispositionStatementAsync"/> <b>appends</b> a new <see cref="Disposition"/>
/// audit row. Prior dispositions for the same finding or job are never modified or deleted.
/// Correcting a decision means recording a new row; the audit trail shows the full history.
/// </para>
/// </remarks>
public interface IDispositionService
{
    /// <summary>
    /// Records a human-reviewer decision for a specific compliance <see cref="Finding"/>
    /// within a <see cref="VerificationJob"/>.
    /// </summary>
    /// <param name="jobId">Identifier of the parent verification job.</param>
    /// <param name="findingId">Identifier of the finding being dispositioned.</param>
    /// <param name="action">The human reviewer's accept or reject decision.</param>
    /// <param name="actor">
    /// Non-empty identifier of the human reviewer (e.g. username or email).
    /// Returns a failure result immediately when null or whitespace.
    /// </param>
    /// <param name="notes">Optional free-text reasoning supplied by the reviewer.</param>
    /// <param name="beforeState">
    /// Optional snapshot of the finding state before the decision (for audit trail).
    /// </param>
    /// <param name="afterState">
    /// Optional snapshot of the finding state after the decision (for audit trail).
    /// </param>
    /// <param name="engineVersion">
    /// Optional semantic version of the verification engine that produced the finding.
    /// </param>
    /// <param name="referenceBundleVersion">
    /// Optional version of the reference-data bundle active when the job was verified.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the appended <see cref="Disposition"/> row on success,
    /// or a failure result when validation fails or a persistence error occurs.
    /// </returns>
    Task<Result<Disposition>> DispositionFindingAsync(
        Guid jobId,
        Guid findingId,
        DispositionAction action,
        string actor,
        string? notes = null,
        string? beforeState = null,
        string? afterState = null,
        string? engineVersion = null,
        string? referenceBundleVersion = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a human-reviewer decision at the aggregate statement level for a
    /// <see cref="VerificationJob"/> (no specific finding — the whole verdict is dispositioned).
    /// </summary>
    /// <param name="jobId">Identifier of the verification job whose verdict is dispositioned.</param>
    /// <param name="action">The human reviewer's accept or reject decision.</param>
    /// <param name="actor">
    /// Non-empty identifier of the human reviewer.
    /// Returns a failure result immediately when null or whitespace.
    /// </param>
    /// <param name="notes">Optional free-text reasoning supplied by the reviewer.</param>
    /// <param name="beforeState">
    /// Optional snapshot of the statement verdict before the decision (for audit trail).
    /// </param>
    /// <param name="afterState">
    /// Optional snapshot of the statement verdict after the decision (for audit trail).
    /// </param>
    /// <param name="engineVersion">
    /// Optional semantic version of the verification engine.
    /// </param>
    /// <param name="referenceBundleVersion">
    /// Optional version of the reference-data bundle.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the appended <see cref="Disposition"/> row on success,
    /// or a failure result when validation fails or a persistence error occurs.
    /// </returns>
    Task<Result<Disposition>> DispositionStatementAsync(
        Guid jobId,
        DispositionAction action,
        string actor,
        string? notes = null,
        string? beforeState = null,
        string? afterState = null,
        string? engineVersion = null,
        string? referenceBundleVersion = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all <see cref="Disposition"/> audit rows recorded for the given
    /// verification job, ordered chronologically.
    /// </summary>
    /// <param name="jobId">Identifier of the verification job.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing an ordered read-only list. Empty list (not failure)
    /// when no dispositions exist yet.
    /// </returns>
    Task<Result<IReadOnlyList<Disposition>>> GetDispositionsForJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}
