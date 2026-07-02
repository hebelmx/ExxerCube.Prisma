namespace ExxerCube.Prisma.Domain.Llm;

/// <summary>
/// Flags that describe the capabilities offered by an LLM provider.
/// </summary>
[Flags]
public enum LlmCapabilities
{
    /// <summary>No capabilities declared.</summary>
    None = 0,

    /// <summary>Provider can generate text completions.</summary>
    TextGenerate = 1 << 0,

    /// <summary>Provider can process images alongside a prompt.</summary>
    VisionGenerate = 1 << 1,

    /// <summary>Provider supports structured / JSON schema output mode.</summary>
    StructuredOutput = 1 << 2,
}
