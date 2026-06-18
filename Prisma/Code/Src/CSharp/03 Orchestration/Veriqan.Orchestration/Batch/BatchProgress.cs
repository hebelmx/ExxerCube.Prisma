namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Snapshot of batch-processing progress reported via <see cref="System.IProgress{T}"/> callbacks
/// as each item completes.
/// </summary>
/// <param name="Total">Total number of submissions in the batch.</param>
/// <param name="Pending">Items not yet started.</param>
/// <param name="InProgress">Items currently being processed.</param>
/// <param name="Completed">
/// Items that produced a <see cref="Pipeline.VerificationOutcome"/> (Green, Red, or Blocked).
/// </param>
/// <param name="Failed">
/// Items that could not be processed (placed on the exception queue).
/// </param>
/// <param name="Blocked">
/// Subset of <see cref="Completed"/> where the verdict signal is
/// <see cref="ExxerCube.Prisma.Veriqan.Domain.Enums.VerdictSignal.Blocked"/>.
/// </param>
public sealed record BatchProgress(
    int Total,
    int Pending,
    int InProgress,
    int Completed,
    int Failed,
    int Blocked);
