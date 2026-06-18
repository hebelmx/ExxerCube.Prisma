using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Final summary produced by <see cref="IBatchProcessor"/> after all items in a batch
/// have been processed (or placed on the exception queue).
/// </summary>
/// <param name="Outcomes">
/// All <see cref="VerificationOutcome"/> instances produced by items that completed
/// the pipeline successfully (including Blocked verdicts).
/// </param>
/// <param name="ExceptionQueue">
/// Items that could not be processed due to a pipeline failure or unexpected exception.
/// </param>
/// <param name="TotalSubmitted">Total number of submissions in the original batch.</param>
/// <param name="CompletedCount">
/// Number of items that produced a <see cref="VerificationOutcome"/> (Green + Red + Blocked).
/// </param>
/// <param name="BlockedCount">
/// Subset of <see cref="CompletedCount"/> where the verdict is
/// <see cref="ExxerCube.Prisma.Veriqan.Domain.Enums.VerdictSignal.Blocked"/>.
/// </param>
/// <param name="FailedCount">Number of items placed on the exception queue.</param>
/// <param name="GreenCount">
/// Number of completed items whose verdict signal is
/// <see cref="ExxerCube.Prisma.Veriqan.Domain.Enums.VerdictSignal.Green"/>.
/// </param>
/// <param name="RedCount">
/// Number of completed items whose verdict signal is
/// <see cref="ExxerCube.Prisma.Veriqan.Domain.Enums.VerdictSignal.Red"/>.
/// </param>
public sealed record BatchReport(
    IReadOnlyList<VerificationOutcome> Outcomes,
    IReadOnlyList<ExceptionQueueEntry> ExceptionQueue,
    int TotalSubmitted,
    int CompletedCount,
    int BlockedCount,
    int FailedCount,
    int GreenCount,
    int RedCount);
