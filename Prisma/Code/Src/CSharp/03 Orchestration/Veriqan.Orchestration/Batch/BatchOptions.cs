namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Configuration options for a <see cref="IBatchProcessor"/> run.
/// </summary>
/// <param name="MaxDegreeOfParallelism">
/// Maximum number of statements processed concurrently.
/// Values less than 1 are clamped to 1.
/// Defaults to 4.
/// </param>
/// <param name="Resume">
/// When <see langword="true"/> the processor skips any statement whose content hash is
/// already recorded as completed in the
/// <see cref="ExxerCube.Prisma.Veriqan.Orchestration.Reprocess.IVerificationResultStore"/>,
/// counting each skip as <see cref="BatchReport.AlreadyCompletedCount"/> rather than
/// re-running the pipeline.  This enables resuming an interrupted batch from the point
/// where it stopped (FR-22).
/// <para>
/// When <see langword="false"/> (the default) every statement is processed unconditionally,
/// matching the original Story 8.1 behaviour.
/// </para>
/// </param>
public sealed record BatchOptions(int MaxDegreeOfParallelism = 4, bool Resume = false)
{
    /// <summary>
    /// Gets the effective maximum degree of parallelism, clamped to at least 1.
    /// </summary>
    public int EffectiveParallelism => MaxDegreeOfParallelism < 1 ? 1 : MaxDegreeOfParallelism;
}
