using Microsoft.Extensions.AI;
using TransformersSharp.Pipelines;

namespace TransformersSharp.MEAI;

/// <summary>
/// A chat client that uses a text generation pipeline to generate responses.
/// </summary>
public class TextGenerationPipelineChatClient : IChatClient
{
    /// <summary>
    /// The text generation pipeline used by this chat client.
    /// </summary>
    public TextGenerationPipeline? TextGenerationPipeline { get; private set; }

    /// <summary>
    /// Create a chat client from a text generation pipeline.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="torchDtype"></param>
    /// <param name="device"></param>
    /// <param name="trustRemoteCode"></param>
    /// <returns></returns>
    public static TextGenerationPipelineChatClient FromModel(string model, TorchDtype? torchDtype = null, string? device = null, bool trustRemoteCode = false)
    {
        return new TextGenerationPipelineChatClient
        {
            TextGenerationPipeline = TextGenerationPipeline.FromModel(model, torchDtype, device, trustRemoteCode: trustRemoteCode)
        };
    }

    /// <summary>
    /// Dispose the chat client and release resources.
    /// </summary>
    public void Dispose()
    {
        // Nothing to do yet.
    }

    /// <summary>
    /// Get a response from the chat client.
    /// </summary>
    /// <param name="messages"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var result = TextGenerationPipeline?.Generate(messages.Select(
                message => new Dictionary<string, string>
                {
                    { "role", message.Role.Value },
                    { "content", message.Text }
                }).ToList(),
                maxNewTokens: options?.MaxOutputTokens,
                topk: options?.TopK,
                topp: options?.TopP,
                temperature: options?.Temperature,
                stopStrings: options?.StopSequences?.AsReadOnly()
                );
            var responseMessages = result?.Select(message => new ChatMessage(new ChatRole(message["role"]), message["content"])).ToList() ?? new List<ChatMessage>();
            return new ChatResponse(responseMessages);
        }, cancellationToken);
    }

    /// <summary>
    /// Get a service from the chat client.
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="serviceKey"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType is null ? throw new ArgumentNullException(nameof(serviceType)) :
        serviceKey is not null ? null :
        serviceType.IsInstanceOfType(this) ? this :
        serviceType == typeof(TextGenerationPipeline) ? this.TextGenerationPipeline :
        null;

    /// <summary>
    /// Get a streaming response from the chat client.
    /// </summary>
    /// <param name="messages"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }
}