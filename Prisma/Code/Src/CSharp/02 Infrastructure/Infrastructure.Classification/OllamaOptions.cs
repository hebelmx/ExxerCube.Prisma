namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Configuration options for the local Ollama LLM service.
/// Bind from the <c>"Ollama"</c> configuration section via
/// <c>services.Configure&lt;OllamaOptions&gt;(config.GetSection(OllamaOptions.SectionName))</c>.
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>The IConfiguration section name to bind this class from.</summary>
    public const string SectionName = "Ollama";

    /// <summary>
    /// Base URL of the Ollama HTTP API, e.g. <c>"http://localhost:11434"</c>.
    /// Must not end with a trailing slash.
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Ollama model tag to use for generation, e.g. <c>"llama3.2:3b"</c>.
    /// </summary>
    public string Model { get; set; } = "llama3.2:3b";

    /// <summary>
    /// Whether the Ollama integration is active.
    /// <c>false</c> (default) means <see cref="OllamaHttpClient"/> is registered but
    /// <see cref="SemanticAnalyzerService"/> silently skips LLM enrichment, so environments
    /// without a running Ollama instance work unchanged.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// HTTP request timeout in seconds for a single generate call (default 60 s).
    /// Set higher for slow hardware or large models.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}
