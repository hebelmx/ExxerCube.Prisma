using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Repository port for persisting and querying <see cref="VerificationJob"/> aggregate roots.
/// Implementations live in the Infrastructure layer and are resolved via dependency injection.
/// </summary>
public interface IVerificationJobRepository
{
    /// <summary>
    /// Finds an existing job by its content hash (SHA-256 hex string).
    /// Returns a successful result containing <c>null</c> when no job exists for the hash,
    /// or a successful result containing the job when one is found.
    /// Returns a failure result on infrastructure errors and a cancelled result when
    /// the cancellation token is already signalled.
    /// </summary>
    /// <param name="contentHash">SHA-256 hex digest of the submitted content (64 hex chars).</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> wrapping the matching job, or <c>null</c> if not found.
    /// </returns>
    Task<Result<VerificationJob?>> FindByContentHashAsync(
        string contentHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new <see cref="VerificationJob"/> to the store and returns the saved entity.
    /// </summary>
    /// <param name="job">The job aggregate to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the persisted job on success.</returns>
    Task<Result<VerificationJob>> AddAsync(
        VerificationJob job,
        CancellationToken cancellationToken = default);
}
