namespace ExxerCube.Prisma.Domain.Llm;

/// <summary>
/// Encapsulates a single request to an LLM provider.
/// </summary>
/// <param name="SystemPrompt">Instruction text that frames the model's role and output contract.</param>
/// <param name="UserPrompt">Document text or user turn that the model should process.</param>
/// <param name="Images">
/// Optional list of raw PNG/JPEG byte arrays for vision-capable providers.
/// When present the provider should select its vision model.
/// </param>
/// <param name="JsonSchema">
/// Optional JSON schema string that the provider should enforce on its output
/// (used when the provider advertises <see cref="LlmCapabilities.StructuredOutput"/>).
/// </param>
/// <param name="ModelOverride">
/// Optional per-request model tag that overrides the provider's configured default.
/// </param>
public sealed record LlmRequest(
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyList<byte[]>? Images = null,
    string? JsonSchema = null,
    string? ModelOverride = null);
