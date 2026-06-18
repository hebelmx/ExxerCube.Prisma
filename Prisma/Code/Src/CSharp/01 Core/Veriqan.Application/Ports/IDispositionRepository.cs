using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Repository port for the append-only <see cref="Disposition"/> audit table.
/// </summary>
/// <remarks>
/// <para>
/// <b>Append-only contract (AR-9):</b> This interface exposes only <see cref="AppendAsync"/>
/// (insert) and <see cref="GetForJobAsync"/> (read). No update or delete operations exist —
/// the underlying store must never modify or remove existing rows. To record a corrected
/// decision, callers append a new <see cref="Disposition"/> row alongside the original.
/// </para>
/// <para>
/// Implementations live in the Infrastructure layer and are resolved via dependency injection.
/// </para>
/// </remarks>
public interface IDispositionRepository
{
    /// <summary>
    /// Appends a single <see cref="Disposition"/> audit row to the immutable store.
    /// </summary>
    /// <remarks>
    /// This is an insert-only operation. Existing rows are never touched.
    /// Returns a failure result on infrastructure errors and a cancelled result when the
    /// cancellation token is already signalled.
    /// </remarks>
    /// <param name="disposition">The disposition audit row to append.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the persisted <see cref="Disposition"/> on success.
    /// </returns>
    Task<Result<Disposition>> AppendAsync(
        Disposition disposition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all <see cref="Disposition"/> audit rows for the specified verification job,
    /// ordered by <see cref="Disposition.DispositionedAtUtc"/> ascending.
    /// </summary>
    /// <param name="jobId">Identifier of the parent <see cref="VerificationJob"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing an ordered read-only list of dispositions.
    /// Returns an empty list (not a failure) when no dispositions exist for the job.
    /// </returns>
    Task<Result<IReadOnlyList<Disposition>>> GetForJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}
