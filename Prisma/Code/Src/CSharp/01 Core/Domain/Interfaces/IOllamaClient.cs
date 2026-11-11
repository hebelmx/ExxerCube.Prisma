using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Port for a local Ollama LLM text generation endpoint.
/// Implementations send a prompt to the configured Ollama instance and return the generated text.
/// The model and endpoint are resolved from configuration; callers supply only the prompt.
/// </summary>
/// <remarks>
/// Fail-open contract: any HTTP, timeout, or parse error returns a failure result
/// instead of throwing. Callers must treat a failure result as "LLM unavailable" and fall back
/// to the structured (regex) result — they must never propagate the failure as a hard error.
/// </remarks>
public interface IOllamaClient
{
    /// <summary>
    /// Sends <paramref name="prompt"/> to the Ollama generate endpoint and returns the generated text.
    /// </summary>
    /// <param name="prompt">The full prompt text to send (system + user context combined).</param>
    /// <param name="cancellationToken">Cancellation token to abort the HTTP request.</param>
    /// <returns>
    /// A success result containing the trimmed response text on success;
    /// a failure result on any transport, timeout, or parse error (fail-open).
    /// </returns>
    Task<Result<string>> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}
