using ExxerCube.Prisma.Domain.Llm;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Orchestrates the hybrid (deterministic + optional LLM) field-extraction pipeline over a PDF document.
/// Runs up to three tracks (deterministic, LLM-text, LLM-vision), then reconciles their results.
/// </summary>
/// <remarks>
/// Ships DARK: the LLM tracks are enabled only when the corresponding flags in
/// <c>LlmProviders:TextExtractorEnabled</c> / <c>LlmProviders:VisionExtractorEnabled</c>
/// are set to <see langword="true"/>. The deterministic track always runs.
/// </remarks>
public interface IHybridExtractionService
{
    /// <summary>
    /// Extracts fields from a PDF document by running the enabled tracks and reconciling results.
    /// </summary>
    /// <param name="pdfBytes">The raw PDF bytes.  Must not be null or empty.</param>
    /// <param name="documentId">
    /// Logical identifier for the document (e.g. a storage key) used for logging and as
    /// the <see cref="Domain.Sources.ImageSource.DocumentId"/> when converting pages to images.
    /// </param>
    /// <param name="visionModelOverride">
    /// Optional Ollama vision-model tag (e.g. <c>granite3.2-vision</c>) that overrides the
    /// configured <c>LlmProviders:Ollama:VisionModel</c> for the LLM-vision track of THIS run only.
    /// Lets the demo swap the vision model at runtime without a restart. <see langword="null"/>
    /// keeps the configured default. The text track is unaffected.
    /// </param>
    /// <param name="cancellationToken">Propagated to all downstream async calls.</param>
    /// <returns>
    /// A successful <see cref="ReconciliationResult"/> on the happy path, or a failure result
    /// when the input is invalid or every extraction track fails.
    /// </returns>
    Task<Result<ReconciliationResult>> ExtractAsync(
        byte[] pdfBytes,
        string documentId,
        string? visionModelOverride = null,
        CancellationToken cancellationToken = default);
}
