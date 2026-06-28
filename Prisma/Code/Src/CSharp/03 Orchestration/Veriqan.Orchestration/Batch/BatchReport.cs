using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Final summary produced by <see cref="IBatchProcessor"/> after all items in a batch
/// have been processed (or placed on the exception queue).
/// </summary>
/// <param name="Outcomes">
/// All <see cref="VerificationOutcome"/> instances produced by items that completed
/// the pipeline successfully during this run (including Blocked verdicts).
/// Does not include items that were skipped due to resume.
/// </param>
/// <param name="ExceptionQueue">
/// Items that could not be processed due to a pipeline failure or unexpected exception.
/// </param>
/// <param name="TotalSubmitted">Total number of submissions in the original batch.</param>
/// <param name="CompletedCount">
/// Number of items that produced a <see cref="VerificationOutcome"/> during this run
/// (Green + Yellow + Red + Blocked).  Does not include <see cref="AlreadyCompletedCount"/>.
/// </param>
/// <param name="BlockedCount">
/// Subset of <see cref="CompletedCount"/> where the verdict is
/// <see cref="ExxerCube.Prisma.Veriqan.Domain.Enums.VerdictSignal.Blocked"/>.
/// Reserved — always zero after Story 4.2 until document-defect detection is wired.
/// </param>
/// <param name="ExtractionGapCount">
/// Subset of <see cref="CompletedCount"/> where the outcome is
/// <see cref="ExxerCube.Prisma.Veriqan.Domain.Enums.VerdictSignal.ExtractionGap"/>
/// (permanent system/capability gap; not a compliance verdict).
/// These items are persisted as a final non-verdict outcome but excluded from
/// compliance pass/fail tallies (<see cref="GreenCount"/>, <see cref="RedCount"/>,
/// <see cref="YellowCount"/>).
/// </param>
/// <param name="TransientFailureCount">
/// Subset of <see cref="CompletedCount"/> where the outcome is
/// <see cref="ExxerCube.Prisma.Veriqan.Domain.Enums.VerdictSignal.TransientFailure"/>
/// (retryable operational failure; not a compliance verdict).
/// These items are retryable and excluded from compliance tallies.
/// Always zero after Story 4.2 until transient-failure detection is wired.
/// </param>
/// <param name="FailedCount">Number of items placed on the exception queue.</param>
/// <param name="GreenCount">
/// Number of completed items whose verdict signal is
/// <see cref="ExxerCube.Prisma.Veriqan.Domain.Enums.VerdictSignal.Green"/>.
/// </param>
/// <param name="YellowCount">
/// Number of completed items whose verdict signal is
/// <see cref="ExxerCube.Prisma.Veriqan.Domain.Enums.VerdictSignal.Yellow"/>
/// (bank improvement opportunities; CONDUSEF regulatory compliance is GREEN).
/// </param>
/// <param name="RedCount">
/// Number of completed items whose verdict signal is
/// <see cref="ExxerCube.Prisma.Veriqan.Domain.Enums.VerdictSignal.Red"/>.
/// </param>
/// <param name="AlreadyCompletedCount">
/// Number of items skipped because their content hash was already present in the
/// <see cref="ExxerCube.Prisma.Veriqan.Orchestration.Reprocess.IVerificationResultStore"/>
/// (only non-zero when <see cref="BatchOptions.Resume"/> is <see langword="true"/>).
/// Resuming a fully-completed batch produces <c>AlreadyCompletedCount == TotalSubmitted</c>
/// and zero pipeline invocations — the idempotent case.
/// </param>
/// <param name="ThroughputPerSecond">
/// Statements completed per second over the whole-batch wall-clock duration.
/// Zero when no items completed or elapsed time was effectively zero.
/// NFR-4 observability metric.
/// </param>
/// <param name="P95LatencyMs">
/// 95th-percentile per-statement processing latency in milliseconds, computed from the
/// <see cref="VerificationOutcome.ProcessingDuration"/> values of completed items.
/// <c>null</c> when no items carried a measured duration (e.g. legacy outcomes or zero
/// completions).  NFR-1 observability metric.
/// </param>
public sealed record BatchReport(
    IReadOnlyList<VerificationOutcome> Outcomes,
    IReadOnlyList<ExceptionQueueEntry> ExceptionQueue,
    int TotalSubmitted,
    int CompletedCount,
    int BlockedCount,
    int ExtractionGapCount,
    int TransientFailureCount,
    int FailedCount,
    int GreenCount,
    int YellowCount,
    int RedCount,
    int AlreadyCompletedCount = 0,
    double ThroughputPerSecond = 0.0,
    double? P95LatencyMs = null);
