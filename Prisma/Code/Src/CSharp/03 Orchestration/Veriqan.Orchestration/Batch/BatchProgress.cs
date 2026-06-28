namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Snapshot of batch-processing progress reported via <see cref="System.IProgress{T}"/> callbacks
/// as each item completes.
/// </summary>
/// <param name="Total">Total number of submissions in the batch.</param>
/// <param name="Pending">Items not yet started (excludes already-completed skips).</param>
/// <param name="InProgress">Items currently being processed.</param>
/// <param name="Completed">
/// Items that produced a <see cref="Pipeline.VerificationOutcome"/> (Green, Red, or Blocked)
/// during this run.
/// </param>
/// <param name="Failed">
/// Items that could not be processed (placed on the exception queue).
/// </param>
/// <param name="Blocked">
/// Subset of <see cref="Completed"/> where the outcome signal is a non-verdict
/// (<see cref="ExxerCube.Prisma.Veriqan.Domain.Enums.VerdictSignal.Blocked"/> or
/// <see cref="ExxerCube.Prisma.Veriqan.Domain.Enums.VerdictSignal.ExtractionGap"/>).
/// Progress-tracking granularity: both are counted here for simplicity.
/// Use <see cref="BatchReport"/> for the per-signal breakdown.
/// </param>
/// <param name="AlreadyCompleted">
/// Items skipped because their content hash was already recorded as completed in the
/// <see cref="ExxerCube.Prisma.Veriqan.Orchestration.Reprocess.IVerificationResultStore"/>
/// (only non-zero when <see cref="BatchOptions.Resume"/> is <see langword="true"/>).
/// </param>
public sealed record BatchProgress(
    int Total,
    int Pending,
    int InProgress,
    int Completed,
    int Failed,
    int Blocked,
    int AlreadyCompleted = 0);
