using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;

/// <summary>
/// Service that forcefully re-runs the verification pipeline for a single statement,
/// replaces the stored prior outcome, and writes an append-only audit entry.
/// </summary>
/// <remarks>
/// <para>
/// <b>Human-actor requirement:</b> Every reprocess call must carry a non-empty
/// <c>actor</c> string identifying the human operator who initiated it.
/// A blank or whitespace actor is rejected with a typed failure — automated pipelines
/// must not call this service without an explicit human identity.
/// </para>
/// <para>
/// <b>No-duplicate guarantee:</b> After a successful reprocess there is exactly one
/// <see cref="VerificationOutcome"/> entry in <see cref="IVerificationResultStore"/>
/// for the statement's content hash (the prior result is replaced, not augmented).
/// The audit trail lives in <see cref="IReprocessAuditRepository"/> and is append-only.
/// </para>
/// </remarks>
public interface IReprocessService
{
    /// <summary>
    /// Re-runs the full verification pipeline for <paramref name="submission"/>, replaces
    /// the prior stored outcome, and appends a <see cref="ReprocessAuditEntry"/> capturing
    /// the before-verdict, after-verdict, actor, and timestamp.
    /// </summary>
    /// <param name="submission">The PDF bytes and context key to reprocess.</param>
    /// <param name="actor">
    /// Non-empty identifier of the human operator (e.g. username or email).
    /// Returns a failure result immediately when null or whitespace.
    /// </param>
    /// <param name="reason">Optional free-text reason for the reprocess.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the new <see cref="VerificationOutcome"/>
    /// on success; a typed failure when <paramref name="actor"/> is blank; a failure on pipeline
    /// or store errors; a cancelled result when <paramref name="ct"/> is signalled.
    /// </returns>
    Task<Result<VerificationOutcome>> ReprocessAsync(
        StatementSubmission submission,
        string actor,
        string? reason,
        CancellationToken ct = default);
}
