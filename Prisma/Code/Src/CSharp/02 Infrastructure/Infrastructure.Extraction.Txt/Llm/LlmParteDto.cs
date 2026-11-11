using System.Text.Json.Serialization;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Txt.Llm;

/// <summary>
/// JSON DTO for a single party extracted by the LLM.
/// </summary>
public sealed record LlmParteDto(
    [property: JsonPropertyName("nombre")] string? Nombre,
    [property: JsonPropertyName("rfc")] string? Rfc,
    [property: JsonPropertyName("curp")] string? Curp,
    [property: JsonPropertyName("fechaNacimiento")] string? FechaNacimiento,
    [property: JsonPropertyName("caracter")] string? Caracter);
