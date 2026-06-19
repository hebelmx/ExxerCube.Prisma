namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Configuration options for a <see cref="IBatchProcessor"/> run.
/// </summary>
/// <param name="MaxDegreeOfParallelism">
/// Maximum number of statements processed concurrently for THIS call.
/// Values less than 1 are clamped to 1. Defaults to
/// <see cref="DefaultMaxDegreeOfParallelism"/> (4); when left at the default the
/// processor instead uses the configured global
/// <c>Veriqan:BatchProcessor:MaxConcurrency</c>, so a non-default value here is an
/// explicit per-call override.
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
public sealed record BatchOptions(int MaxDegreeOfParallelism = 4 /* == DefaultMaxDegreeOfParallelism */, bool Resume = false)
{
    /// <summary>
    /// The default per-call parallelism (must match the primary-constructor default above).
    /// A <see cref="MaxDegreeOfParallelism"/> equal to this value is treated as "unset" — the
    /// processor falls back to the configured global <c>Veriqan:BatchProcessor:MaxConcurrency</c>.
    /// </summary>
    public const int DefaultMaxDegreeOfParallelism = 4;

    /// <summary>
    /// Gets the effective maximum degree of parallelism, clamped to at least 1.
    /// </summary>
    public int EffectiveParallelism => MaxDegreeOfParallelism < 1 ? 1 : MaxDegreeOfParallelism;
}
