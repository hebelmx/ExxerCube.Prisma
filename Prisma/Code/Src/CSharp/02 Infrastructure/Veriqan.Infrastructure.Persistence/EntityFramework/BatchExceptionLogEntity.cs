namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;

/// <summary>
/// EF persistence entity for a single row in the durable batch-processing dead-letter log
/// (VERIQAN-E3-S3). Each row records one <c>BatchProcessor</c> item that failed processing
/// — either a pipeline <c>Result</c> failure or an unhandled exception — so failures survive
/// process restarts and can be triaged via <c>GET /exceptions</c>.
/// </summary>
public sealed class BatchExceptionLogEntity
{
    /// <summary>Gets the unique identifier for this dead-letter row.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the batch run that produced this failure.</summary>
    public Guid BatchId { get; init; }

    /// <summary>Gets the SHA-256 hex digest of the failed submission's PDF bytes.</summary>
    public string StatementHash { get; init; } = string.Empty;

    /// <summary>Gets the institution identifier (bank/issuer name) the submission targeted.</summary>
    public string InstitutionId { get; init; } = string.Empty;

    /// <summary>Gets the human-readable failure reason, truncated to <c>BatchExceptionLogEntry.MaxFailureReasonLength</c>.</summary>
    public string FailureReason { get; init; } = string.Empty;

    /// <summary>Gets the UTC timestamp at which the failure occurred.</summary>
    public DateTimeOffset FailedAt { get; init; }

    /// <summary>
    /// Gets the number of times an operator has retried this item. Mutable triage field;
    /// defaults to zero on first insert.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Gets the UTC timestamp of the most recent retry, or <see langword="null"/> if the item
    /// has never been retried. Mutable triage field.
    /// </summary>
    public DateTimeOffset? LastRetryAt { get; init; }

    /// <summary>
    /// Gets the UTC timestamp the item was marked resolved, or <see langword="null"/> if it is
    /// still open. Mutable triage field.
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; init; }
}
