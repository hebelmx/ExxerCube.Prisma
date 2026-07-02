using ExxerCube.Prisma.Domain.Entities;

namespace ExxerCube.Prisma.Domain.Llm;

/// <summary>
/// A single extraction attempt paired with its source label and outcome status.
/// The collection of labelled extractions is the input to <see cref="Interfaces.IExtractionReconciler"/>.
/// </summary>
/// <param name="Source">
/// Identifies the extractor that produced this candidate: <c>"deterministic"</c>,
/// <c>"llm-text"</c>, <c>"llm-vision"</c>, etc.  The reconciler uses this label to
/// apply the merge policy (deterministic non-null wins over any LLM value).
/// </param>
/// <param name="Fields">
/// The <see cref="Expediente"/> populated by the extractor, or <see langword="null"/> when
/// <see cref="Status"/> is not <see cref="TrackStatus.Available"/>.
/// </param>
/// <param name="Status">Why the track succeeded or was skipped/failed.</param>
/// <param name="Note">Optional human-readable note (error message, skip reason, etc.).</param>
public sealed record LabelledExtraction(
    string Source,
    Expediente? Fields,
    TrackStatus Status,
    string? Note = null);
