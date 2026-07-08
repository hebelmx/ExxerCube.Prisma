namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Identifies one rung of the per-field progressive fallback-extraction chain
/// (see <c>docs/planning-artifacts/veriqan-fallback-chain-design-2026-07-05.md</c>).
/// </summary>
/// <remarks>
/// A lightweight, value-equatable wrapper around a stable string identifier — cheap to
/// construct, compare, and carry on <see cref="ExtractionProvenance"/>. The well-known stages
/// are exposed as static properties; callers should prefer those over constructing ad-hoc
/// values so that provenance data stays comparable across the codebase.
/// </remarks>
/// <param name="Value">The stable identifier for this stage (e.g. <c>"Positional"</c>).</param>
public readonly record struct StageId(string Value)
{
    /// <summary>
    /// Stage 1 — the existing exact/positional extractor (<c>PdfPigStatementFieldExtractor</c>).
    /// </summary>
    public static StageId Positional { get; } = new("Positional");

    /// <summary>
    /// Stage 2 — fuzzy label matching (e.g. FuzzySharp) locates a label phrase that positional
    /// exact-match missed, then reads the value band relative to it.
    /// </summary>
    public static StageId FuzzyLabel { get; } = new("FuzzyLabel");

    /// <summary>
    /// Stage 3 — character edit-distance comparison, used for token garbling or
    /// context disambiguation that fuzzy label matching alone cannot resolve.
    /// </summary>
    public static StageId Levenshtein { get; } = new("Levenshtein");

    /// <summary>
    /// Stage 4 — embedding-based semantic search locates the <em>concept</em> on the page
    /// and hands a narrowed window downstream.
    /// </summary>
    public static StageId SemanticSearch { get; } = new("SemanticSearch");

    /// <summary>
    /// Stage 5 — last-resort LLM extraction over a bounded window with a strict
    /// single-field JSON schema.
    /// </summary>
    public static StageId LlmExtraction { get; } = new("LlmExtraction");

    /// <summary>
    /// Header-image OCR stage: renders page 1, crops the top fractional header band, and reads
    /// a product-name heading via Tesseract OCR when the positional extractor found nothing (see
    /// <c>docs/planning-artifacts/veriqan-e7-s72-header-ocr-design-2026-07-08.md</c>). As of
    /// E7.S7.2/S7.3 the only field registered against this stage is <c>FieldKind.Product</c>.
    /// </summary>
    public static StageId HeaderImageOcr { get; } = new("HeaderImageOcr");

    /// <inheritdoc/>
    public override string ToString() => Value;
}
