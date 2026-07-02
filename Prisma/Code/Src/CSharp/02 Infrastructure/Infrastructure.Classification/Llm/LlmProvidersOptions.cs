namespace ExxerCube.Prisma.Infrastructure.Classification.Llm;

/// <summary>
/// Top-level options for the LLM provider subsystem.
/// Bind from the <c>"LlmProviders"</c> configuration section.
/// </summary>
public sealed class LlmProvidersOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "LlmProviders";

    /// <summary>
    /// Name of the provider that is active when no runtime override has been set.
    /// Defaults to <c>"Ollama"</c>.
    /// </summary>
    public string Active { get; set; } = "Ollama";

    /// <summary>
    /// When <see langword="false"/> (default) the LLM text extractor is registered as a
    /// concrete scoped service but the active <see cref="Domain.Interfaces.IFieldExtractor{T}"/>
    /// binding remains the deterministic adapter — ships dark.
    /// </summary>
    public bool TextExtractorEnabled { get; set; } = false;

    /// <summary>
    /// When <see langword="false"/> (default) the LLM vision extractor ships dark.
    /// </summary>
    public bool VisionExtractorEnabled { get; set; } = false;

    /// <summary>Options for the local Ollama back-end.</summary>
    public OllamaProviderOptions Ollama { get; set; } = new();

    /// <summary>Options for the Google Gemini back-end.</summary>
    public GeminiProviderOptions Gemini { get; set; } = new();
}

/// <summary>
/// Per-provider options for the local Ollama server.
/// </summary>
public sealed class OllamaProviderOptions
{
    /// <summary>
    /// Base URL of the Ollama HTTP API.  Must NOT end with a trailing slash.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Default text model tag.</summary>
    public string Model { get; set; } = "llama3.2";

    /// <summary>Vision model tag (used when <see cref="Domain.Llm.LlmRequest.Images"/> is non-empty).</summary>
    public string VisionModel { get; set; } = "minicpm-v";

    /// <summary>HTTP request timeout in seconds for a single generate call.</summary>
    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// Per-provider options for Google Gemini.
/// </summary>
public sealed class GeminiProviderOptions
{
    /// <summary>
    /// IConfiguration key from which the API key is read at runtime
    /// (user-secrets / env var).  Never stored in plain text.
    /// </summary>
    public string ApiKeyRef { get; set; } = "Gemini:ApiKey";

    /// <summary>Gemini model name.</summary>
    public string Model { get; set; } = "gemini-2.0-flash";

    /// <summary>HTTP request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
