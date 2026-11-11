using ExxerCube.Prisma.Domain.Llm;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Abstraction for an LLM back-end that can generate text from a structured request.
/// Implementations live in Infrastructure; the Domain only knows the port.
/// </summary>
public interface ILlmProvider
{
    /// <summary>Gets the provider's canonical name (e.g. "Ollama", "Gemini").</summary>
    string Name { get; }

    /// <summary>Gets the set of capabilities this provider supports.</summary>
    LlmCapabilities Capabilities { get; }

    /// <summary>
    /// Sends <paramref name="request"/> to the LLM back-end and returns the raw text response.
    /// </summary>
    /// <param name="request">The prompt and optional images/schema for this call.</param>
    /// <param name="cancellationToken">Propagated to the underlying HTTP call.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the model's raw response text,
    /// or a failure if the HTTP call or parsing fails.
    /// </returns>
    Task<Result<string>> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default);
}
