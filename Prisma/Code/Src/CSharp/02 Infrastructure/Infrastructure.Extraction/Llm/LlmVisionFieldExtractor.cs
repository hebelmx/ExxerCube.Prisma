using System.Text.Json;
using ExxerCube.Prisma.Domain.Llm;
using ExxerCube.Prisma.Infrastructure.Extraction.Txt.Llm;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Llm;

/// <summary>
/// LLM-backed field extractor for <see cref="ImageSource"/> (rasterised document pages).
/// Sends the page images to the active <see cref="ILlmProvider"/> as vision input,
/// validates the response through <see cref="LlmExtractionGate"/>, and maps it to
/// a full <see cref="Expediente"/> (including <c>SolicitudPartes</c>) via
/// <see cref="LlmExpedienteMapper"/>.
/// </summary>
/// <remarks>
/// Ships DARK: registered as a concrete scoped service but does NOT replace the active
/// <c>IFieldExtractor&lt;PdfSource&gt;</c> or any other deterministic binding until an
/// upstream orchestrator explicitly routes an <see cref="ImageSource"/> to this extractor.
/// <para>
/// Implements both <see cref="IFieldExtractor{ImageSource}"/> (backward compat) and
/// <see cref="ILlmExpedienteExtractor{ImageSource}"/> (returns the full
/// <see cref="Expediente"/> with partes so they survive into reconciliation).
/// The canonical implementation is <see cref="ExtractExpedienteAsync"/>; <see cref="ExtractFieldsAsync"/>
/// delegates to it and wraps the result into <see cref="ExtractedFields"/> for compat.
/// </para>
/// The shared DTO/gate/mapper from <c>Infrastructure.Extraction.Txt</c> are reused verbatim;
/// only the provenance marker (<c>_ExtractionSource=llm-vision</c>) differs from the text path.
/// </remarks>
public sealed class LlmVisionFieldExtractor : IFieldExtractor<ImageSource>, ILlmExpedienteExtractor<ImageSource>
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

    // -----------------------------------------------------------------------
    // ILlmExpedienteExtractor<ImageSource> — canonical implementation
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    /// <remarks>
    /// Sends page images to the active vision-capable <see cref="ILlmProvider"/>,
    /// validates the response through <see cref="LlmExtractionGate"/>, and maps it to a full
    /// <see cref="Expediente"/> (including <c>SolicitudPartes</c>) via <see cref="LlmExpedienteMapper"/>.
    /// Provider resolution and network calls are guarded by try/catch — never throws.
    /// No <c>CancellationToken</c> is propagated to the provider (passes <c>CancellationToken.None</c>
    /// — mirrors the established text-extractor contract).
    /// </remarks>
    public async Task<Result<Expediente>> ExtractExpedienteAsync(
        ImageSource source,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
            return Result<Expediente>.WithFailure("ImageSource cannot be null.");

        if (source.PagePngs is null || source.PagePngs.Count == 0)
            return Result<Expediente>.WithFailure("ImageSource contains no page images.");

        // Resolve INSIDE a guard: GetActive() throws on a misconfigured LlmProviders:Active,
        // and this method must never throw — convert to WithFailure.
        ILlmProvider provider;
        try
        {
            provider = _providerFactory.GetActive();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LlmVisionFieldExtractor: provider resolution failed unexpectedly.");
            return Result<Expediente>.WithFailure($"LLM provider resolution failed: {ex.Message}");
        }

        if (!provider.Capabilities.HasFlag(LlmCapabilities.VisionGenerate))
        {
            return Result<Expediente>.WithFailure(
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
            return Result<Expediente>.WithFailure($"LLM provider call failed: {ex.Message}");
        }

        if (!llmResult.IsSuccess)
        {
            _logger.LogWarning(
                "LlmVisionFieldExtractor: provider returned failure — {Error}",
                llmResult.Errors?.FirstOrDefault());
            return Result<Expediente>.WithFailure(
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
            return Result<Expediente>.WithFailure($"JSON parse error: {ex.Message}");
        }

        if (!LlmExtractionGate.IsValid(dto, out var reason))
        {
            _logger.LogWarning("LlmVisionFieldExtractor: gate rejected DTO — {Reason}", reason);
            return Result<Expediente>.WithFailure($"Gate rejected LLM output: {reason}");
        }

        var expediente = LlmExpedienteMapper.ToExpediente(dto!);
        // Override provenance marker — the mapper sets "llm-text"; correct it for this track.
        expediente.AdditionalFields["_ExtractionSource"] = "llm-vision";

        _logger.LogInformation(
            "LlmVisionFieldExtractor: extraction complete — Expediente={Expediente}, Partes={ParteCount}",
            expediente.NumeroExpediente, expediente.SolicitudPartes.Count);

        return Result<Expediente>.WithSuccess(expediente);
    }

    // -----------------------------------------------------------------------
    // IFieldExtractor<ImageSource> — delegates to ExtractExpedienteAsync and wraps
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    /// <remarks>
    /// Delegates to <see cref="ExtractExpedienteAsync"/> and wraps the returned
    /// <see cref="Expediente"/> into an <see cref="ExtractedFields"/> value object for
    /// backward compatibility.
    /// Note: <c>SolicitudPartes</c> are NOT present in the returned <see cref="ExtractedFields"/>;
    /// use <see cref="ExtractExpedienteAsync"/> when partes must survive.
    /// </remarks>
    public async Task<Result<ExtractedFields>> ExtractFieldsAsync(
        ImageSource source,
        FieldDefinition[] fieldDefinitions)
    {
        var expResult = await ExtractExpedienteAsync(source, CancellationToken.None)
            .ConfigureAwait(false);

        if (!expResult.IsSuccess)
        {
            return Result<ExtractedFields>.WithFailure(
                expResult.Errors?.FirstOrDefault() ?? "LLM vision extraction failed");
        }

        var expediente = expResult.Value!;

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
          "expediente": "<número de expediente en formato CNBV: Letra/Letras-dígitos-dígitos-Letras (formato ilustrativo X/XX0-0000-000000-XXX). Copia el valor EXACTO del documento; o null si no aparece. No inventes ni copies el ejemplo.>",
          "solicitante": "<nombre completo del solicitante o null>",
          "monto": "<monto en formato numérico decimal invariante, por ejemplo 1234.56, o null>",
          "cuenta": "<número de cuenta bancaria o null>",
          "rfc": "<RFC en formato estándar mexicano o null>",
          "curp": "<CURP o null>",
          "numeroOficio": "<número de oficio en formato LETRAS/AAAA/NNNNNN (formato ilustrativo XXXX/0000/000000). Copia el valor EXACTO del documento; o null si no aparece. No inventes ni copies el ejemplo.>",
          "autoridadNombre": "<nombre completo de la autoridad emisora tal como aparece en el documento, o null si no aparece. No inventes valores.>",
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
