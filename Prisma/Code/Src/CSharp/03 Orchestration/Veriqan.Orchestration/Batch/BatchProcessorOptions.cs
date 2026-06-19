using System;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Configuration options for the streaming <see cref="BatchProcessor"/>.
/// </summary>
/// <remarks>
/// Bound from the <c>Veriqan:BatchProcessor</c> configuration section.
/// </remarks>
public sealed class BatchProcessorOptions
{
    /// <summary>Configuration section path.</summary>
    public const string Section = "Veriqan:BatchProcessor";

    /// <summary>
    /// Default maximum number of concurrent consumer workers.
    /// Defaults to <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    public static readonly int DefaultMaxConcurrency = Environment.ProcessorCount;

    /// <summary>
    /// Maximum number of consumer workers that process statement-submission items
    /// concurrently from the channel.  Values less than 1 are clamped to 1.
    /// Defaults to <see cref="DefaultMaxConcurrency"/> (<see cref="Environment.ProcessorCount"/>).
    /// </summary>
    public int MaxConcurrency { get; set; } = DefaultMaxConcurrency;

    /// <summary>
    /// Gets the effective concurrency, clamped to at least 1.
    /// </summary>
    public int EffectiveConcurrency => MaxConcurrency < 1 ? 1 : MaxConcurrency;
}
