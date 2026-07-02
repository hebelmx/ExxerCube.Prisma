namespace ExxerCube.Prisma.Domain.Llm;

/// <summary>
/// Describes why a particular extraction track produced (or did not produce) a result.
/// Used in <see cref="LabelledExtraction"/> so the reconciler and callers understand the provenance
/// of each candidate without inspecting the <c>Fields</c> value.
/// </summary>
public enum TrackStatus
{
    /// <summary>The track ran successfully and produced a candidate result.</summary>
    Available,

    /// <summary>
    /// The track was skipped because the active LLM provider does not advertise the required
    /// capability (e.g. vision extraction when <c>VisionGenerate</c> is absent).
    /// </summary>
    SkippedNoCapability,

    /// <summary>
    /// The track was skipped because a feature flag (e.g. <c>VisionExtractorEnabled=false</c>)
    /// is set to disable it for the current session.
    /// </summary>
    SkippedByFlag,

    /// <summary>The track ran but encountered an unrecoverable error.</summary>
    Failed,
}
