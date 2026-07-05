namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Records how an <see cref="ExtractedField{T}"/> value was obtained — which resolution
/// <see cref="StageId"/> produced it, and (for the LLM stage only) content-hash diagnostics
/// for determinism replay and audit.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. <see cref="ModelTag"/>, <see cref="PromptHash"/>, and <see cref="ResponseHash"/>
/// are <see langword="null"/> for every non-LLM stage, including <see cref="Positional"/>.
/// </para>
/// <para>
/// IMPORTANT: this type intentionally carries only content <em>hashes</em>, never the raw
/// prompt or response text. Do not add a raw-prompt/raw-response field here — logging or
/// serializing this record (including via its auto-generated <c>ToString</c>/<c>PrintMembers</c>)
/// must never be able to leak model input/output content (lesson from Veriqan Epic 6.6).
/// </para>
/// </remarks>
/// <param name="Stage">The resolution stage that produced the field value.</param>
/// <param name="ModelTag">Pinned model identifier (e.g. provider/tag) for the LLM stage; <see langword="null"/> otherwise.</param>
/// <param name="PromptHash">Content hash of the prompt sent to the LLM stage; <see langword="null"/> otherwise.</param>
/// <param name="ResponseHash">Content hash of the response received from the LLM stage; <see langword="null"/> otherwise.</param>
public sealed record ExtractionProvenance(
    StageId Stage,
    string? ModelTag = null,
    string? PromptHash = null,
    string? ResponseHash = null)
{
    /// <summary>
    /// The default provenance for values produced by the positional extractor (stage 1) —
    /// the only stage that exists as of E1. All diagnostic fields are <see langword="null"/>.
    /// </summary>
    public static ExtractionProvenance Positional { get; } = new(StageId.Positional);
}
