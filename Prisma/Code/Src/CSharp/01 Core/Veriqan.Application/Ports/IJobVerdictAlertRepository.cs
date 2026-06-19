using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Repository port for reading and updating the alert-sent flag on <see cref="JobVerdict"/>.
/// </summary>
/// <remarks>
/// This port is a narrow slice of the full verdict lifecycle, focused exclusively on the
/// duplicate-alert guard (FR-17 idempotency, Story E2-S15).  Implementations live in the
/// Infrastructure layer and are resolved via dependency injection.
/// </remarks>
public interface IJobVerdictAlertRepository
{
    /// <summary>
    /// Loads the <see cref="JobVerdict"/> identified by <paramref name="verdictId"/>.
    /// </summary>
    /// <param name="verdictId">The verdict identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the verdict when found, <see langword="null"/> when
    /// no verdict exists for the given id, or a failure result on infrastructure errors.
    /// </returns>
    Task<Result<JobVerdict?>> FindByIdAsync(
        Guid verdictId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the <see cref="JobVerdict.AlertSentAt"/> value set by
    /// <see cref="JobVerdict.RecordAlertSent"/>.
    /// </summary>
    /// <param name="verdict">The verdict whose <see cref="JobVerdict.AlertSentAt"/> was just set.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// A success <see cref="Result"/> when the flag was written; a failure result on
    /// infrastructure or concurrency errors; a cancelled result when the token fires.
    /// </returns>
    Task<Result> SaveAlertSentAsync(
        JobVerdict verdict,
        CancellationToken cancellationToken = default);
}
