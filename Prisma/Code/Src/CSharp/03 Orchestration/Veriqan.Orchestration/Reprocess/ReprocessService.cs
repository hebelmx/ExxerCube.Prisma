using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;

/// <summary>
/// Default <see cref="IReprocessService"/> implementation that forces a full pipeline re-run
/// for a single statement, replaces the prior stored outcome, and appends an audit entry.
/// </summary>
/// <remarks>
/// <para>
/// <b>Human-actor invariant:</b> A non-empty <c>actor</c> is required; blank actors are rejected
/// with a typed failure and no pipeline run or store write occurs.
/// </para>
/// <para>
/// <b>No-duplicate guarantee:</b> <see cref="IVerificationResultStore.ReplaceOutcomeAsync"/> is
/// called, which overwrites the prior entry in place.  After a successful reprocess exactly one
/// outcome exists in the store for the content hash.
/// </para>
/// </remarks>
internal sealed class ReprocessService : IReprocessService
{
    private readonly IVerificationPipeline _pipeline;
    private readonly IVerificationResultStore _store;
    private readonly IReprocessAuditRepository _audit;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReprocessService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ReprocessService"/>.
    /// </summary>
    public ReprocessService(
        IVerificationPipeline pipeline,
        IVerificationResultStore store,
        IReprocessAuditRepository audit,
        TimeProvider timeProvider,
        ILogger<ReprocessService> logger)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<VerificationOutcome>> ReprocessAsync(
        StatementSubmission submission,
        string actor,
        string? reason,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<VerificationOutcome>();

        // Human-actor invariant: reject automated/blank callers immediately
        if (string.IsNullOrWhiteSpace(actor))
        {
            _logger.LogWarning(
                "Reprocess rejected for {FileName}: actor is null or whitespace.",
                submission.FileName);
            return Result<VerificationOutcome>.WithFailure(
                "A non-empty actor is required. Reprocess must be initiated by a human operator.");
        }

        // Compute the content hash to use as the store key (same algorithm as StatementIngestionService)
        var contentHash = ComputeSha256Hex(submission.Pdf);

        _logger.LogInformation(
            "Reprocess started for {FileName} (hash {ContentHash}) by {Actor}.",
            submission.FileName, contentHash, actor);

        // Capture the PRIOR outcome (before-state for audit)
        var priorResult = await _store.GetOutcomeAsync(contentHash, ct).ConfigureAwait(false);

        if (priorResult.IsCancelled())
            return ResultExtensions.Cancelled<VerificationOutcome>();

        if (priorResult.IsFailure)
        {
            _logger.LogWarning(
                "Could not read prior outcome for hash {ContentHash}: {Error}",
                contentHash, priorResult.Error);
            return Result<VerificationOutcome>.WithFailure(
                priorResult.Error ?? "Failed to read prior outcome.");
        }

        var priorOutcome = priorResult.Value;           // may be null — first-time reprocess
        var beforeSignal = priorOutcome?.Summary.Signal;

        // Force re-run the full pipeline (ignores completion state)
        var pipelineResult = await _pipeline.ProcessAsync(submission, ct).ConfigureAwait(false);

        if (pipelineResult.IsCancelled())
            return ResultExtensions.Cancelled<VerificationOutcome>();

        if (pipelineResult.IsFailure)
        {
            _logger.LogWarning(
                "Reprocess pipeline failed for {FileName}: {Error}",
                submission.FileName, pipelineResult.Error);
            return Result<VerificationOutcome>.WithFailure(
                pipelineResult.Error ?? "Reprocess pipeline failed.");
        }

        var newOutcome = pipelineResult.Value!;

        // Replace the prior result in the store (no duplicate — exactly one entry per hash)
        var replaceResult = await _store.ReplaceOutcomeAsync(contentHash, newOutcome, ct)
            .ConfigureAwait(false);

        if (replaceResult.IsCancelled())
            return ResultExtensions.Cancelled<VerificationOutcome>();

        if (replaceResult.IsFailure)
        {
            _logger.LogError(
                "Failed to persist reprocess outcome for hash {ContentHash}: {Error}",
                contentHash, replaceResult.Error);
            return Result<VerificationOutcome>.WithFailure(
                replaceResult.Error ?? "Failed to persist reprocess outcome.");
        }

        // Append an audit entry (append-only — never overwrites prior audits)
        var auditEntry = new ReprocessAuditEntry(
            Id: Guid.NewGuid(),
            ContentHash: contentHash,
            Actor: actor,
            Reason: reason,
            BeforeSignal: beforeSignal,
            AfterSignal: newOutcome.Summary.Signal,
            ReprocessedAtUtc: _timeProvider.GetUtcNow());

        var appendResult = await _audit.AppendAsync(auditEntry, ct).ConfigureAwait(false);

        if (appendResult.IsCancelled())
            return ResultExtensions.Cancelled<VerificationOutcome>();

        if (appendResult.IsFailure)
        {
            // Audit failure is treated as an error — the outcome is already stored but the
            // audit trail is incomplete.  Return failure so callers can take corrective action.
            _logger.LogError(
                "Reprocess audit append failed for hash {ContentHash}: {Error}",
                contentHash, appendResult.Error);
            return Result<VerificationOutcome>.WithFailure(
                appendResult.Error ?? "Failed to append reprocess audit entry.");
        }

        _logger.LogInformation(
            "Reprocess complete for {FileName} (hash {ContentHash}). Before={BeforeSignal} After={AfterSignal} Actor={Actor}.",
            submission.FileName, contentHash,
            beforeSignal?.ToString() ?? "<none>",
            newOutcome.Summary.Signal,
            actor);

        return Result<VerificationOutcome>.WithSuccess(newOutcome);
    }

    /// <summary>
    /// Computes the SHA-256 digest of <paramref name="bytes"/> and returns it as a lowercase
    /// 64-character hex string. Mirrors <c>StatementIngestionService.ComputeSha256Hex</c>.
    /// </summary>
    private static string ComputeSha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
