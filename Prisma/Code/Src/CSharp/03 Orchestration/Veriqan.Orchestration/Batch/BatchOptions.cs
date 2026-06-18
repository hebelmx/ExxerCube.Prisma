namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Configuration options for a <see cref="IBatchProcessor"/> run.
/// </summary>
/// <param name="MaxDegreeOfParallelism">
/// Maximum number of statements processed concurrently.
/// Values less than 1 are clamped to 1.
/// Defaults to 4.
/// </param>
public sealed record BatchOptions(int MaxDegreeOfParallelism = 4)
{
    /// <summary>
    /// Gets the effective maximum degree of parallelism, clamped to at least 1.
    /// </summary>
    public int EffectiveParallelism => MaxDegreeOfParallelism < 1 ? 1 : MaxDegreeOfParallelism;
}
