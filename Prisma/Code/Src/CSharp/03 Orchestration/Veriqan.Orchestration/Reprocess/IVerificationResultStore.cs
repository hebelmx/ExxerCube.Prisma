using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;

/// <summary>
/// Application port for persisting and querying <see cref="VerificationOutcome"/> records keyed
/// by statement content hash.  Supports idempotent batch resume (FR-22) and explicit reprocess.
/// </summary>
/// <remarks>
/// <para>
/// <b>Completion key:</b> The natural key is the SHA-256 content hash that
/// <see cref="ExxerCube.Prisma.Veriqan.Application.Ports.IStatementIngestionService"/> computes
/// during ingestion.  The same hash that deduplicates jobs also drives completion checks, so
/// re-submitting identical bytes is a no-op.
/// </para>
/// <para>
/// <b>First-write-wins for <see cref="SaveOutcomeAsync"/>:</b> Calling
/// <see cref="SaveOutcomeAsync"/> when a record already exists for the content hash
/// is silently accepted — the existing record is left intact.
/// Use <see cref="ReplaceOutcomeAsync"/> to deliberately overwrite a prior result (reprocess path).
/// </para>
/// <para>
/// <b>No duplicates on replace:</b> <see cref="ReplaceOutcomeAsync"/> overwrites the live record
/// in place — after a reprocess there is exactly one outcome entry for each content hash.
/// The before/after audit is written separately via <see cref="IReprocessAuditRepository"/>.
/// </para>
/// </remarks>
public interface IVerificationResultStore
{
    /// <summary>
    /// Returns <see langword="true"/> when a completed outcome already exists for
    /// <paramref name="contentHash"/>, <see langword="false"/> otherwise.
    /// </summary>
    /// <param name="contentHash">SHA-256 hex digest of the statement content (64 hex chars).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> wrapping the completion flag;
    /// a cancelled result when <paramref name="ct"/> is signalled;
    /// a failure result on infrastructure errors.
    /// </returns>
    Task<Result<bool>> IsCompletedAsync(string contentHash, CancellationToken ct = default);

    /// <summary>
    /// Persists <paramref name="outcome"/> for <paramref name="contentHash"/> when no record yet
    /// exists.  Idempotent: a second call with the same hash is a silent no-op that leaves the
    /// existing record unchanged.
    /// </summary>
    /// <param name="contentHash">SHA-256 hex digest identifying the statement.</param>
    /// <param name="outcome">The <see cref="VerificationOutcome"/> to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A successful <see cref="Result"/> on success (including no-op); cancelled or failure on error.</returns>
    Task<Result> SaveOutcomeAsync(string contentHash, VerificationOutcome outcome, CancellationToken ct = default);

    /// <summary>
    /// Replaces the stored outcome for <paramref name="contentHash"/> with <paramref name="outcome"/>.
    /// Creates a new record when none exists.  Used exclusively by the reprocess path — exactly
    /// one outcome entry exists per hash after the call (no duplicate is created).
    /// </summary>
    /// <param name="contentHash">SHA-256 hex digest identifying the statement.</param>
    /// <param name="outcome">The new <see cref="VerificationOutcome"/> to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A successful <see cref="Result"/> on success; cancelled or failure on error.</returns>
    Task<Result> ReplaceOutcomeAsync(string contentHash, VerificationOutcome outcome, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the stored <see cref="VerificationOutcome"/> for <paramref name="contentHash"/>,
    /// or <see langword="null"/> when no record exists.
    /// </summary>
    /// <param name="contentHash">SHA-256 hex digest identifying the statement.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> wrapping the outcome, or <see langword="null"/> when
    /// not found; a cancelled result when <paramref name="ct"/> is signalled; failure on errors.
    /// </returns>
    Task<Result<VerificationOutcome?>> GetOutcomeAsync(string contentHash, CancellationToken ct = default);
}
