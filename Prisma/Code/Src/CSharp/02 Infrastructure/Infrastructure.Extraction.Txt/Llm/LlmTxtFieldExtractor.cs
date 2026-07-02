using System.Text.Json;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Llm;
using ExxerCube.Prisma.Infrastructure.Classification.Llm;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Txt.Llm;

/// <summary>
/// LLM-backed field extractor for <see cref="TxtSource"/>.
/// Ships DARK: registered as a concrete scoped service but does NOT replace the active
/// <c>IFieldExtractor&lt;TxtSource&gt;</c> binding until <c>LlmProviders:TextExtractorEnabled=true</c>.
/// </summary>
public sealed class LlmTxtFieldExtractor : IFieldExtractor<TxtSource>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILlmProviderFactory _providerFactory;
    private readonly LlmProvidersOptions _options;
    private readonly ILogger<LlmTxtFieldExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="LlmTxtFieldExtractor"/>.
    /// </summary>
    /// <param name="providerFactory">Factory used to retrieve the active LLM provider.</param>
    /// <param name="options">Current LLM provider options snapshot.</param>
    /// <param name="logger">Structured logger.</param>
    public LlmTxtFieldExtractor(
        ILlmProviderFactory providerFactory,
        IOptions<LlmProvidersOptions> options,
        ILogger<LlmTxtFieldExtractor> logger)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sends the OCR text to the active LLM provider with a Spanish extraction prompt,
    /// validates the response through <see cref="LlmExtractionGate"/>, and maps it to
    /// <see cref="ExtractedFields"/> via <see cref="LlmExpedienteMapper"/>.
    /// The <paramref name="fieldDefinitions"/> parameter is accepted for interface compatibility
    /// but the LLM prompt always extracts the full <see cref="LlmExpedienteDto"/> schema.
    /// </remarks>
    public async Task<Result<ExtractedFields>> ExtractFieldsAsync(
        TxtSource source,
        FieldDefinition[] fieldDefinitions)
    {
        if (source is null)
        {
            return Result<ExtractedFields>.WithFailure("TxtSource cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(source.TextContent))
        {
            return Result<ExtractedFields>.WithFailure("TxtSource.TextContent cannot be null or empty.");
        }

        var systemPrompt = BuildSystemPrompt();
        var request = new LlmRequest(systemPrompt, source.TextContent);

        // Resolve the provider INSIDE the try: GetActive() throws on a misconfigured
        // LlmProviders:Active, and IFieldExtractor must never throw — convert to WithFailure.
        Result<string> llmResult;
        try
        {
            var provider = _providerFactory.GetActive();
            _logger.LogDebug(
                "LlmTxtFieldExtractor: calling provider '{Provider}' with {Length} chars of OCR text.",
                provider.Name,
                source.TextContent.Length);
            llmResult = await provider
                .GenerateAsync(request, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LlmTxtFieldExtractor: provider resolution/call failed unexpectedly.");
            return Result<ExtractedFields>.WithFailure($"LLM provider call failed: {ex.Message}");
        }

        if (!llmResult.IsSuccess)
        {
            _logger.LogWarning("LlmTxtFieldExtractor: provider returned failure — {Error}",
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
            _logger.LogWarning(ex, "LlmTxtFieldExtractor: JSON parse failed. Raw response: {Raw}",
                llmResult.Value);
            return Result<ExtractedFields>.WithFailure($"JSON parse error: {ex.Message}");
        }

        if (!LlmExtractionGate.IsValid(dto, out var reason))
        {
            _logger.LogWarning("LlmTxtFieldExtractor: gate rejected DTO — {Reason}", reason);
            return Result<ExtractedFields>.WithFailure($"Gate rejected LLM output: {reason}");
        }

        var expediente = LlmExpedienteMapper.ToExpediente(dto!);

        // Build ExtractedFields from the mapped Expediente.
        var fields = new ExtractedFields
        {
            Expediente = string.IsNullOrEmpty(expediente.NumeroExpediente)
                ? null
                : expediente.NumeroExpediente,
        };

        // Carry provenance and other mapped values into AdditionalFields.
        foreach (var kv in expediente.AdditionalFields)
        {
            fields.AdditionalFields[kv.Key] = kv.Value;
        }

        if (!string.IsNullOrWhiteSpace(expediente.NombreSolicitante))
        {
            fields.AdditionalFields["NombreSolicitante"] = expediente.NombreSolicitante;
        }

        // Carry OCR provenance so the reconciliator can weight the signal.
        if (source.OcrConfidence.HasValue)
        {
            fields.AdditionalFields["_OcrConfidence"] =
                source.OcrConfidence.Value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(source.TextContent))
        {
            fields.AdditionalFields["_OcrText"] = source.TextContent;
        }

        _logger.LogInformation(
            "LlmTxtFieldExtractor: extraction complete — Expediente={Expediente}, Partes={ParteCount}",
            expediente.NumeroExpediente,
            expediente.SolicitudPartes.Count);

        return Result<ExtractedFields>.WithSuccess(fields);
    }

    /// <inheritdoc />
    public Task<Result<FieldValue>> ExtractFieldAsync(TxtSource source, string fieldName)
    {
        // Single-field extraction is not supported for the LLM path; callers should use
        // ExtractFieldsAsync and pick the desired field from the result.
        return Task.FromResult(
            Result<FieldValue>.WithFailure(
                "LlmTxtFieldExtractor does not support single-field extraction. " +
                "Use ExtractFieldsAsync instead."));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string BuildSystemPrompt() =>
        """
        Eres un asistente experto en extracción de datos de documentos legales mexicanos.
        Tu única tarea es analizar el texto que el usuario te proporcionará y devolver
        EXCLUSIVAMENTE un objeto JSON con la siguiente estructura (usa null para los campos
        que no puedas identificar en el texto):

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
