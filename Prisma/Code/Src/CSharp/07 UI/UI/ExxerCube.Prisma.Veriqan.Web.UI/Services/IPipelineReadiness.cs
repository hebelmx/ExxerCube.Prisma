namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// Shared, thread-safe "is the pipeline warmed up" bit, set once by
/// <see cref="PipelineWarmupHostedService"/>'s background warm-up task.
/// </summary>
/// <remarks>
/// No existing readiness-flag convention was found elsewhere in the repo (the CLAUDE.md
/// "readiness probes present but stubbed" note refers to a different, TODO'd
/// <c>orchestrator.IsStarted</c> probe in the Worker hosts) — this is a small, new,
/// single-purpose singleton scoped to this Web.UI demo surface only.
/// </remarks>
public interface IPipelineReadiness
{
    /// <summary>
    /// Gets whether the pipeline warm-up has completed successfully at least once.
    /// <see langword="false"/> until the first successful warm-up; never reverts to
    /// <see langword="false"/> afterwards (a later warm-up failure does not un-ready it —
    /// this is a "has it ever proven it can run" bit, not a live health check).
    /// </summary>
    bool IsReady { get; }

    /// <summary>Marks the pipeline as ready. Idempotent; safe to call from any thread.</summary>
    void MarkReady();
}
