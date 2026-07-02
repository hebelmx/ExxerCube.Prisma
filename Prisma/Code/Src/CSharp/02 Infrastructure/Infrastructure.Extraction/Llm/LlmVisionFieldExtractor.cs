using System.Text.Json;
using ExxerCube.Prisma.Domain.Llm;
using ExxerCube.Prisma.Infrastructure.Extraction.Txt.Llm;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Llm;

/// <summary>
/// LLM-backed field extractor for <see cref="ImageSource"/> (rasterised document pages).
/// Sends the page images to the active <see cref="ILlmProvider"/> as vision input,
/// validates the response through <see cref="LlmExtractionGate"/>, and maps it to
/// <see cref="ExtractedFields"/> via <see cref="LlmExpedienteMapper"/>.
/// </summary>
/// <remarks>
/// Ships DARK: registered as a concrete scoped service but does NOT replace the active
/// <c>IFieldExtractor&lt;PdfSource&gt;</c> or any other deterministic binding until an
/// upstream orchestrator explicitly routes an <see cref="ImageSource"/> to this extractor.
/// The shared DTO/gate/mapper from <c>Infrastructure.Extraction.Txt</c> are reused verbatim;
/// only the provenance marker (<c>_ExtractionSource=llm-vision</c>) differs from the text path.
/// </remarks>
public sealed class LlmVisionFieldExtractor : IFieldExtractor<ImageSource>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILlmProviderFactory _providerFactory;
    private readonly ILogger<LlmVisionFieldExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="LlmVisionFieldExtractor"/>.
    /// </summary>
    /// <param name="providerFactory">Resolves the active <see cref="ILlmProvider"/> at call time.</param>
    /// <param name="logger">Structured logger.</param>
    public LlmVisionFieldExtractor(
        ILlmProviderFactory providerFactory,
        ILogger<LlmVisionFieldExtractor> logger)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <remarks>
    /// The <paramref name="fieldDefinitions"/> parameter is accepted for interface compatibility
    /// but the LLM prompt always extracts the full <see cref="LlmExpedienteDto"/> schema.
    /// No <c>CancellationToken</c> on the interface — passes <c>CancellationToken.None</c> to
    /// the provider (mirrors the text-extractor contract).
    /// </remarks>
    public async Task<Result<ExtractedFields>> ExtractFieldsAsync(
        ImageSource source,
        FieldDefinition[] fieldDefinitions)
    {
        if (source is null)
            return Result<ExtractedFields>.WithFailure("ImageSource cannot be null.");

        if (source.PagePngs is null || source.PagePngs.Count == 0)
            return Result<ExtractedFields>.WithFailure("ImageSource contains no page images.");

        // Resolve INSIDE a guard: GetActive() throws on a misconfigured LlmProviders:Active,
        // and IFieldExtractor must never throw — convert to WithFailure.
        ILlmProvider provider;
        try
        {
            provider = _providerFactory.GetActive();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LlmVisionFieldExtractor: provider resolution failed unexpectedly.");
            return Result<ExtractedFields>.WithFailure($"LLM provider resolution failed: {ex.Message}");
        }

        if (!provider.Capabilities.HasFlag(LlmCapabilities.VisionGenerate))
        {
            return Result<ExtractedFields>.WithFailure(
                $"Active provider '{provider.Name}' does not support vision " +
                "(VisionGenerate capability is required for image-based extraction).");
        }

        var request = new LlmRequest(
            BuildSystemPrompt(),
            "Extrae los campos de este documento.",
            Images: source.PagePngs);

        _logger.LogDebug(
            "LlmVisionFieldExtractor: calling provider '{Provider}' with {PageCount} page(s) " +
            "for document '{DocumentId}'.",
            provider.Name, source.PagePngs.Count, source.DocumentId);

        Result<string> llmResult;
        try
        {
            llmResult = await provider
                .GenerateAsync(request, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LlmVisionFieldExtractor: provider threw unexpectedly.");
            return Result<ExtractedFields>.WithFailure($"LLM provider call failed: {ex.Message}");
        }

        if (!llmResult.IsSuccess)
        {
            _logger.LogWarning(
                "LlmVisionFieldExtractor: provider returned failure — {Error}",
                llmResult.Errors?.FirstOrDefault());
            return Result<ExtractedFields>.WithFailure(
                $"LLM provider failed: {llmResult.Errors?.FirstOrDefault() ?? "unknown error"}");
        }

        LlmExpedienteDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<LlmExpedienteDto>(llmResult.Value!, JsonOpts);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex, "LlmVisionFieldExtractor: JSON parse failed. Raw: {Raw}", llmResult.Value);
            return Result<ExtractedFields>.WithFailure($"JSON parse error: {ex.Message}");
        }

        if (!LlmExtractionGate.IsValid(dto, out var reason))
        {
            _logger.LogWarning("LlmVisionFieldExtractor: gate rejected DTO — {Reason}", reason);
            return Result<ExtractedFields>.WithFailure($"Gate rejected LLM output: {reason}");
        }

        var expediente = LlmExpedienteMapper.ToExpediente(dto!);

        // Override provenance marker — the mapper sets "llm-text"; correct it for this track.
        expediente.AdditionalFields["_ExtractionSource"] = "llm-vision";

        // Build ExtractedFields from the mapped Expediente (same shape as the text extractor).
        var fields = new ExtractedFields
        {
            Expediente = string.IsNullOrEmpty(expediente.NumeroExpediente)
                ? null
                : expediente.NumeroExpediente,
        };

        foreach (var kv in expediente.AdditionalFields)
        {
            fields.AdditionalFields[kv.Key] = kv.Value;
        }

        if (!string.IsNullOrWhiteSpace(expediente.NombreSolicitante))
        {
            fields.AdditionalFields["NombreSolicitante"] = expediente.NombreSolicitante;
        }

        _logger.LogInformation(
            "LlmVisionFieldExtractor: extraction complete — Expediente={Expediente}, Partes={ParteCount}",
            expediente.NumeroExpediente, expediente.SolicitudPartes.Count);

        return Result<ExtractedFields>.WithSuccess(fields);
    }

    /// <inheritdoc />
    public Task<Result<FieldValue>> ExtractFieldAsync(ImageSource source, string fieldName)
    {
        return Task.FromResult(
            Result<FieldValue>.WithFailure(
                "LlmVisionFieldExtractor does not support single-field extraction. " +
                "Use ExtractFieldsAsync instead."));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string BuildSystemPrompt() =>
        """
        Eres un asistente experto en extracción de datos de documentos legales mexicanos.
        Tu única tarea es analizar las imágenes de documento que se te proporcionarán y devolver
        EXCLUSIVAMENTE un objeto JSON con la siguiente estructura (usa null para los campos
        que no puedas identificar en las imágenes):

        {
          "expediente": "<número de expediente en formato ddd/yyyy o similar, o null>",
          "solicitante": "<nombre completo del solicitante o null>",
          "monto": "<monto en formato numérico decimal invariante, por ejemplo 1234.56, o null>",
          "cuenta": "<número de cuenta bancaria o null>",
          "rfc": "<RFC en formato estándar mexicano o null>",
          "curp": "<CURP o null>",
          "partes": [
            {
              "nombre": "<nombre completo de la parte o null>",
              "rfc": "<RFC de la parte o null>",
              "curp": "<CURP de la parte o null>",
              "fechaNacimiento": "<fecha de nacimiento en formato ISO 8601 (yyyy-MM-dd) o null>",
              "caracter": "<carácter jurídico de la parte o null>"
            }
          ]
        }

        IMPORTANTE:
        - Responde ÚNICAMENTE con el objeto JSON, sin texto adicional, sin markdown, sin explicaciones.
        - Si no encuentras un campo, usa null (no inventes valores).
        - El arreglo "partes" puede estar vacío ([]) si no hay partes identificadas.
        """;
}
