using System;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Durable dead-letter record for a single batch item that failed processing, persisted so
/// failures survive process restarts and can be triaged after the fact via
/// <c>GET /exceptions</c> (VERIQAN-E3-S3).
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="ExceptionQueueEntry"/> — the in-memory, per-run summary attached to
/// <see cref="BatchReport"/> for the immediate HTTP response — this record is written to the
/// <c>veriqan.BatchExceptionLog</c> table and is the ops source of truth for dead-letter
/// triage.
/// </para>
/// <para>
/// <b>Mutable triage lifecycle:</b> <see cref="RetryCount"/>, <see cref="LastRetryAt"/>, and
/// <see cref="ResolvedAt"/> are intentionally mutable (unlike the append-only audit tables) —
/// they are updated as an operator retries or resolves a dead-lettered item. This story only
/// writes the initial row (RetryCount = 0, LastRetryAt = null, ResolvedAt = null); the
/// retry/resolve write paths are not yet built.
/// </para>
/// </remarks>
/// <param name="Id">Unique identifier of this dead-letter row.</param>
/// <param name="BatchId">
/// Identifier of the <c>ProcessBatchAsync</c> run that produced this failure — generated once
/// per batch call so <c>GET /exceptions?batchId=</c> can scope results to a single run.
/// </param>
/// <param name="StatementHash">
/// SHA-256 hex digest of the failed submission's PDF bytes. Always computable from
/// <see cref="Pipeline.StatementSubmission.Pdf"/>, so never null.
/// </param>
/// <param name="InstitutionId">
/// Institution identifier from <see cref="Pipeline.StatementSubmission.ContextKey"/>. The
/// schema calls this <c>InstitutionId</c>; today's <c>StatementContextKey.Institution</c> is a
/// free-text bank/issuer name rather than a surrogate key, so this column stores that name
/// verbatim. Always computable, so never null.
/// </param>
/// <param name="FailureReason">
/// Human-readable failure description — from <c>Result.Error</c> for pipeline failures, or
/// <see cref="Exception.Message"/> for unexpected exceptions. Capped at
/// <see cref="MaxFailureReasonLength"/> characters; longer messages are truncated before
/// persisting.
/// </param>
/// <param name="FailedAt">UTC timestamp when the failure occurred.</param>
/// <param name="RetryCount">Number of times an operator has retried this item. Defaults to 0.</param>
/// <param name="LastRetryAt">UTC timestamp of the most recent retry, or null if never retried.</param>
/// <param name="ResolvedAt">UTC timestamp the item was marked resolved, or null if still open.</param>
public sealed record BatchExceptionLogEntry(
    Guid Id,
    Guid BatchId,
    string StatementHash,
    string InstitutionId,
    string FailureReason,
    DateTimeOffset FailedAt,
    int RetryCount = 0,
    DateTimeOffset? LastRetryAt = null,
    DateTimeOffset? ResolvedAt = null)
{
    /// <summary>
    /// Maximum persisted length of <see cref="FailureReason"/>. Exception messages (especially
    /// stack-trace-laden ones) can be arbitrarily large; callers must truncate before
    /// constructing this record.
    /// </summary>
    public const int MaxFailureReasonLength = 2000;
}
