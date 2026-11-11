using System.Text.Json.Serialization;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Txt.Llm;

/// <summary>
/// JSON DTO for the complete case-file object extracted by the LLM.
/// All fields are nullable; the extraction gate validates which combinations are acceptable.
/// </summary>
public sealed record LlmExpedienteDto(
    [property: JsonPropertyName("expediente")] string? Expediente,
    [property: JsonPropertyName("solicitante")] string? Solicitante,
    [property: JsonPropertyName("monto")] string? Monto,
    [property: JsonPropertyName("cuenta")] string? Cuenta,
    [property: JsonPropertyName("rfc")] string? Rfc,
    [property: JsonPropertyName("curp")] string? Curp,
    [property: JsonPropertyName("partes")] LlmParteDto[]? Partes,
    [property: JsonPropertyName("numeroOficio")] string? NumeroOficio = null,
    [property: JsonPropertyName("autoridadNombre")] string? AutoridadNombre = null);
